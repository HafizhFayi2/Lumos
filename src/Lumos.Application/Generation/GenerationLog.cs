using System.Collections.Concurrent;

namespace Lumos.Application.Generation;

/// Persistent generation history for a project.
/// Tracks every generation request, its result, and any associated assets.
public sealed class GenerationLog
{
    private readonly ConcurrentDictionary<Guid, List<GenerationLogEntry>> _projectLogs = new();

    /// Record a completed generation in the log.
    public void Record(GenerationJob job)
    {
        var entry = new GenerationLogEntry
        {
            JobId = job.JobId,
            ModelId = job.Request.ModelId,
            Prompt = job.Request.Prompt,
            NegativePrompt = job.Request.NegativePrompt,
            Width = job.Request.Width,
            Height = job.Request.Height,
            DurationSeconds = job.Request.DurationSeconds,
            Succeeded = job.Succeeded,
            AssetId = job.AssetId,
            OutputPath = job.OutputPath,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAt,
        };

        var entries = _projectLogs.GetOrAdd(job.ProjectId, _ => new List<GenerationLogEntry>());
        lock (entries)
        {
            entries.Add(entry);
        }
    }

    /// Get all generation log entries for a project.
    public IReadOnlyList<GenerationLogEntry> GetProjectLog(Guid projectId)
    {
        if (_projectLogs.TryGetValue(projectId, out var entries))
        {
            lock (entries)
            {
                return entries.OrderByDescending(e => e.CreatedAt).ToList();
            }
        }
        return Array.Empty<GenerationLogEntry>();
    }

    /// Get a specific entry by job ID.
    public GenerationLogEntry? GetEntry(string jobId)
    {
        foreach (var (_, entries) in _projectLogs)
        {
            lock (entries)
            {
                var entry = entries.FirstOrDefault(e => e.JobId == jobId);
                if (entry != null) return entry;
            }
        }
        return null;
    }

    /// Clear all logs for a project.
    public void ClearProject(Guid projectId)
    {
        _projectLogs.TryRemove(projectId, out _);
    }
}

/// A single entry in the generation history log.
public sealed record GenerationLogEntry
{
    public string JobId { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public string? NegativePrompt { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? DurationSeconds { get; init; }
    public bool Succeeded { get; init; }
    public string? AssetId { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CreatedAt { get; init; }
}
