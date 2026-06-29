using System.Collections.Concurrent;
using Lumos.Application.Assets;
using Lumos.Domain;

namespace Lumos.Application.Generation;

/// Orchestrates AI media generation: dispatches to providers,
/// tracks status, downloads results, and imports assets.
public sealed class GenerationService
{
    private readonly ModelCatalog _catalog;
    private readonly AssetManager _assetManager;
    private readonly ConcurrentDictionary<string, GenerationJob> _jobs = new();

    public event EventHandler<GenerationStatusEventArgs>? StatusChanged;

    public GenerationService(ModelCatalog catalog, AssetManager assetManager)
    {
        _catalog = catalog;
        _assetManager = assetManager;
    }

    /// Start a generation job. Returns a job ID for status polling.
    public string StartGeneration(GenerationRequest request, Guid projectId)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var job = new GenerationJob(jobId, request, projectId);
        _jobs[jobId] = job;

        // Fire-and-forget the actual generation
        _ = ProcessJobAsync(job);

        return jobId;
    }

    /// Get current status of a generation job.
    public GenerationJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    /// Get all recent jobs.
    public IReadOnlyList<GenerationJob> GetAllJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.CreatedAt).ToList();
    }

    /// Get jobs for a specific project.
    public IReadOnlyList<GenerationJob> GetProjectJobs(Guid projectId)
    {
        return _jobs.Values.Where(j => j.ProjectId == projectId)
            .OrderByDescending(j => j.CreatedAt).ToList();
    }

    private async Task ProcessJobAsync(GenerationJob job)
    {
        try
        {
            var model = _catalog.GetModel(job.Request.ModelId);
            if (model == null)
            {
                job.Fail("Model not found: " + job.Request.ModelId);
                EmitStatus(job);
                return;
            }

            var provider = _catalog.GetProviderForModel(job.Request.ModelId);
            if (provider == null)
            {
                job.Fail("No provider registered for model: " + job.Request.ModelId);
                EmitStatus(job);
                return;
            }

            job.Status = GenerationStatus.Generating;
            EmitStatus(job);

            var progress = new Progress<double>(p => job.Progress = p);
            var result = await provider.GenerateAsync(job.Request, progress, CancellationToken.None);

            if (!result.Succeeded)
            {
                job.Fail(result.ErrorMessage ?? "Generation failed");
                EmitStatus(job);
                return;
            }

            // Download or copy the result
            job.Status = GenerationStatus.Downloading;
            EmitStatus(job);

            string? finalPath = result.OutputPath;
            if (result.RemoteUrl != null && finalPath == null)
            {
                finalPath = await DownloadToTempAsync(result.RemoteUrl, job.Request.ModelId);
            }

            if (finalPath == null || !File.Exists(finalPath))
            {
                job.Fail("Downloaded file not found");
                EmitStatus(job);
                return;
            }

            // Import as asset
            job.Status = GenerationStatus.Rendering;
            EmitStatus(job);

            var asset = await _assetManager.ImportAssetAsync(job.ProjectId, finalPath);
            asset.GenerationStatus = GenerationStatus.None;
            asset.GenerationPrompt = job.Request.Prompt;
            asset.CachedRemoteUrl = result.RemoteUrl;
            asset.CachedRemoteUrlExpiresAt = result.RemoteUrlExpiresAt;

            job.AssetId = asset.Id;
            job.OutputPath = finalPath;
            job.Status = GenerationStatus.None;
            job.Succeeded = true;
            EmitStatus(job);
        }
        catch (Exception ex)
        {
            job.Fail(ex.Message);
            EmitStatus(job);
        }
    }

    private void EmitStatus(GenerationJob job)
    {
        StatusChanged?.Invoke(this, new GenerationStatusEventArgs(
            job.JobId, job.Request.ModelId, job.Request.Prompt, job.Status, job.AssetId));
    }

    private static async Task<string?> DownloadToTempAsync(string url, string modelId)
    {
        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync(url);

            string ext = ".bin";
            var contentType = response.Content.Headers.ContentType?.MediaType;
            ext = contentType switch
            {
                "image/png" => ".png",
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/webp" => ".webp",
                "video/mp4" => ".mp4",
                "audio/mpeg" => ".mp3",
                "audio/wav" => ".wav",
                _ => ".bin",
            };

            var tempPath = Path.Combine(Path.GetTempPath(), $"lumos_gen_{Guid.NewGuid():N}{ext}");
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(tempPath);
            await stream.CopyToAsync(file);

            return tempPath;
        }
        catch
        {
            return null;
        }
    }
}

/// Tracks the lifecycle of a single generation job.
public sealed class GenerationJob
{
    public string JobId { get; }
    public GenerationRequest Request { get; }
    public Guid ProjectId { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public GenerationStatus Status { get; internal set; } = GenerationStatus.Generating;
    public double Progress { get; internal set; }
    public string? AssetId { get; internal set; }
    public string? OutputPath { get; internal set; }
    public string? ErrorMessage { get; internal set; }
    public bool Succeeded { get; internal set; }

    public GenerationJob(string jobId, GenerationRequest request, Guid projectId)
    {
        JobId = jobId;
        Request = request;
        ProjectId = projectId;
    }

    public bool IsCompleted => Status == GenerationStatus.None || Status == GenerationStatus.Failed;

    internal void Fail(string message)
    {
        Status = GenerationStatus.Failed;
        ErrorMessage = message;
        Succeeded = false;
    }
}
