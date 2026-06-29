using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.Generation;
using Lumos.Application.State;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class GenerationTools
{
    private readonly EditorStore _store;
    private readonly GenerationService _generation;
    private readonly ModelCatalog _catalog;
    private readonly GenerationLog _log;
    private readonly AgentActionHistory _actionHistory;

    public GenerationTools(
        EditorStore store,
        GenerationService generation,
        ModelCatalog catalog,
        GenerationLog log,
        AgentActionHistory actionHistory)
    {
        _store = store;
        _generation = generation;
        _catalog = catalog;
        _log = log;
        _actionHistory = actionHistory;
    }

    [McpServerTool(Name = ToolDefinitions.ListModels)]
    [Description("List available AI generation models. Args: type (string, optional — 'image', 'video', 'audio'). Returns list of model IDs and providers.")]
    public Task<string> ListModelsAsync(JsonElement args, CancellationToken ct = default)
    {
        ModelProviderType? typeFilter = null;
        if (McpToolHelpers.TryGetString(args, "type", out var typeStr) && !string.IsNullOrEmpty(typeStr))
        {
            typeFilter = typeStr.ToLowerInvariant() switch
            {
                "image" => ModelProviderType.Image,
                "video" => ModelProviderType.Video,
                "audio" => ModelProviderType.Audio,
                "upscale" => ModelProviderType.Upscale,
                _ => null,
            };
        }

        var models = _catalog.ListModels(typeFilter);
        var modelList = models.Select(m => new
        {
            id = m.Id,
            name = m.Name,
            type = m.Type.ToString().ToLowerInvariant(),
            provider = m.Provider,
            hasApiKey = m.HasApiKey,
        });

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            modelCount = modelList.Count(),
            models = modelList,
        }, McpToolHelpers.DefaultJsonOptions));
    }

    [McpServerTool(Name = ToolDefinitions.GenerateMedia)]
    [Description("Generate media (image, video, or audio) from a text prompt. Args: prompt (string, required), model_id (string, required), negative_prompt (string, optional), width (int, optional), height (int, optional), duration_seconds (int, optional for audio/video). Returns job_id for status polling.")]
    public Task<string> GenerateMediaAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "prompt", out var prompt) || string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult(McpToolHelpers.Error("prompt (string) is required"));

        if (!McpToolHelpers.TryGetString(args, "model_id", out var modelId) || string.IsNullOrWhiteSpace(modelId))
            return Task.FromResult(McpToolHelpers.Error("model_id (string) is required"));

        var model = _catalog.GetModel(modelId);
        if (model == null)
            return Task.FromResult(McpToolHelpers.Error($"model_id '{modelId}' not found. Use list_models to see available models."));

        McpToolHelpers.TryGetString(args, "negative_prompt", out var negativePrompt);
        McpToolHelpers.TryGetInt(args, "width", out var width);
        McpToolHelpers.TryGetInt(args, "height", out var height);
        McpToolHelpers.TryGetInt(args, "duration_seconds", out var durationSeconds);

        var request = new GenerationRequest(
            prompt, modelId, negativePrompt,
            width > 0 ? width : null,
            height > 0 ? height : null,
            durationSeconds > 0 ? durationSeconds : null);

        var projectId = _store.State.ProjectId;
        var jobId = _generation.StartGeneration(request, projectId);

        RecordAction("generate_media", $"Started generation: \"{prompt[..Math.Min(60, prompt.Length)]}\" on {modelId}", true,
            $"jobId={jobId}");

        return Task.FromResult(McpToolHelpers.Ok(new
        {
            jobId,
            modelId,
            prompt,
            status = "generating",
        }));
    }

    [McpServerTool(Name = ToolDefinitions.GetGenerationStatus)]
    [Description("Get the status of a generation job. Args: job_id (string, required). Returns current status, progress, and asset ID when complete.")]
    public Task<string> GetGenerationStatusAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "job_id", out var jobId) || string.IsNullOrWhiteSpace(jobId))
            return Task.FromResult(McpToolHelpers.Error("job_id (string) is required"));

        var job = _generation.GetJob(jobId);
        if (job == null)
            return Task.FromResult(McpToolHelpers.Error($"job_id '{jobId}' not found"));

        // Record to log if completed
        if (job.IsCompleted)
        {
            _log.Record(job);
        }

        return Task.FromResult(McpToolHelpers.Ok(new
        {
            jobId = job.JobId,
            status = job.Status.ToString().ToLowerInvariant(),
            progress = job.Progress,
            assetId = job.AssetId,
            outputPath = job.OutputPath,
            error = job.ErrorMessage,
            succeeded = job.Succeeded,
            isCompleted = job.IsCompleted,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.GetGenerationLog)]
    [Description("Get the generation history log for the current project. Returns recent generation entries with prompts, model IDs, and asset references.")]
    public Task<string> GetGenerationLogAsync(CancellationToken ct = default)
    {
        var projectId = _store.State.ProjectId;
        var entries = _log.GetProjectLog(projectId);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            entryCount = entries.Count,
            entries = entries.Select(e => new
            {
                jobId = e.JobId,
                modelId = e.ModelId,
                prompt = e.Prompt,
                succeeded = e.Succeeded,
                assetId = e.AssetId,
                error = e.ErrorMessage,
                createdAt = e.CreatedAt,
            }),
        }, McpToolHelpers.DefaultJsonOptions));
    }

    [McpServerTool(Name = ToolDefinitions.SetModelApiKey)]
    [Description("Configure an API key for a generation model (BYOK). Args: model_id (string, required), api_key (string, required).")]
    public Task<string> SetModelApiKeyAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "model_id", out var modelId) || string.IsNullOrWhiteSpace(modelId))
            return Task.FromResult(McpToolHelpers.Error("model_id (string) is required"));

        if (!McpToolHelpers.TryGetString(args, "api_key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult(McpToolHelpers.Error("api_key (string) is required"));

        var model = _catalog.GetModel(modelId);
        if (model == null)
            return Task.FromResult(McpToolHelpers.Error($"model_id '{modelId}' not found"));

        _catalog.SetApiKey(modelId, apiKey);

        RecordAction("set_model_api_key", $"Configured API key for model {modelId}", true);

        return Task.FromResult(McpToolHelpers.Ok(new
        {
            modelId,
            configured = true,
        }));
    }

    private void RecordAction(string toolName, string description, bool succeeded, string? details = null)
    {
        _actionHistory.Record(toolName, description, succeeded, details);
    }
}
