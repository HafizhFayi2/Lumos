using Lumos.Domain;

namespace Lumos.Application.Generation;

/// The type of media a model can generate.
public enum ModelProviderType
{
    Image,
    Video,
    Audio,
    Upscale,
}

/// Configuration for a single model that can be registered in the catalog.
public sealed record ModelConfig(
    string Id,
    string Name,
    ModelProviderType Type,
    string Provider,
    string? ApiKey = null,
    string? ApiEndpoint = null)
{
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}

/// Parameters for a single generation request.
public sealed record GenerationRequest(
    string Prompt,
    string ModelId,
    string? NegativePrompt = null,
    int? Width = null,
    int? Height = null,
    int? DurationSeconds = null,
    string? SourceAssetId = null); // For upscale / image-to-video

/// Result of a completed generation.
public sealed record GenerationResult(
    bool Succeeded,
    string? OutputPath,
    string? ErrorMessage,
    string? RemoteUrl = null,
    DateTime? RemoteUrlExpiresAt = null);

/// Status event payload that the GenerationService emits.
public sealed record GenerationStatusEventArgs(
    string RequestId,
    string ModelId,
    string Prompt,
    GenerationStatus Status,
    string? AssetId = null);

/// Interface for a model provider that can generate media.
/// Each provider can support one or more model IDs.
public interface IModelProvider
{
    /// Human-readable provider name (e.g. "Simulated", "Stable Diffusion", "OpenAI").
    string Name { get; }

    /// The type of media this provider generates.
    ModelProviderType Type { get; }

    /// Generate media from a text prompt.
    /// Should report progress via Progress if possible.
    Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
