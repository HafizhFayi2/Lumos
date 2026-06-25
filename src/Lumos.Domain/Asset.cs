namespace Palmier.Domain;

public enum GenerationStatus { None, Generating, Downloading, Rendering, Failed }

public sealed class Asset
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public ClipType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Duration { get; set; }
    public int? SourceWidth { get; set; }
    public int? SourceHeight { get; set; }
    public double? SourceFps { get; set; }
    public bool HasAudio { get; set; }
    public string? FolderId { get; set; }

    // AI generation
    public GenerationStatus GenerationStatus { get; set; } = GenerationStatus.None;
    public string? GenerationPrompt { get; set; }
    public string? CachedRemoteUrl { get; set; }
    public DateTime? CachedRemoteUrlExpiresAt { get; set; }

    public bool IsGenerated => GenerationPrompt is not null;
    public bool IsGenerating => GenerationStatus is
        GenerationStatus.Generating or GenerationStatus.Downloading or GenerationStatus.Rendering;

    public string? FreshRemoteUrl =>
        CachedRemoteUrl is not null &&
        CachedRemoteUrlExpiresAt.HasValue &&
        CachedRemoteUrlExpiresAt.Value > DateTime.UtcNow
            ? CachedRemoteUrl : null;

    public double? AspectRatio =>
        SourceWidth is > 0 && SourceHeight is > 0
            ? (double)SourceWidth.Value / SourceHeight.Value
            : null;
}
