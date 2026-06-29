using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Lumos.Application.Assets;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class AssetTools
{
    private readonly EditorStore _store;
    private readonly CommandQueue _queue;
    private readonly AssetManager _assets;

    public AssetTools(EditorStore store, CommandQueue queue, AssetManager assets)
    {
        _store = store;
        _queue = queue;
        _assets = assets;
    }

    [McpServerTool(Name = ToolDefinitions.ListAssets)]
    [Description("List all media assets referenced by clips in the current project.")]
    public Task<string> ListAssetsAsync(CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        var assets = tl.Tracks
            .SelectMany(t => t.Clips)
            .GroupBy(c => c.MediaRef)
            .Select(g => new
            {
                mediaRef = g.Key,
                type     = g.First().MediaType.ToString(),
                clipCount = g.Count(),
                usedByClipIds = g.Select(c => c.Id).ToList(),
            })
            .ToList();

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            ok = true,
            assetCount = assets.Count,
            assets,
        }, new JsonSerializerOptions
        {
            WriteIndented = false,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.GetMedia)]
    [Description("List all media assets in the project with full metadata (id, path, type, duration, resolution, fps).")]
    public Task<string> GetMediaAsync(CancellationToken ct = default)
    {
        var catalog = _assets.Catalog.GetAll();
        if (catalog.Count == 0)
            return Task.FromResult(McpToolHelpers.Ok(new { assetCount = 0, assets = new List<object>() }));

        var assets = catalog.Select(a => new
        {
            id = a.Id,
            name = a.Name,
            path = a.FilePath,
            type = a.Type.ToString(),
            durationSeconds = a.Duration,
            width = a.SourceWidth,
            height = a.SourceHeight,
            fps = a.SourceFps,
            hasAudio = a.HasAudio,
            folderId = a.FolderId,
            isGenerated = a.IsGenerated,
            generationStatus = a.GenerationStatus.ToString(),
        }).ToList();

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            assetCount = assets.Count,
            assets,
        }, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.InspectMedia)]
    [Description("Return metadata for a specific media asset by ID. Args: asset_id (string).")]
    public Task<string> InspectMediaAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "asset_id", out var assetId) || string.IsNullOrWhiteSpace(assetId))
            return Task.FromResult(McpToolHelpers.Error("asset_id (string) is required"));

        if (!_assets.Catalog.TryGetById(assetId, out var asset) || asset == null)
            return Task.FromResult(McpToolHelpers.Error($"Asset '{assetId}' not found in project catalog."));

        // Find which clips reference this asset
        var tl = _store.State.Timeline.Timeline;
        List<object>? clipList = tl?.Tracks.SelectMany(t => t.Clips)
            .Where(c => c.MediaRef == asset.FilePath)
            .Select<Domain.Clip, object>(c => new { id = c.Id, trackId = tl!.Tracks.FirstOrDefault(tr => tr.Clips.Any(c2 => c2.Id == c.Id))?.Id })
            .ToList();
        var clipRefs = clipList ?? new List<object>();

        var result = new
        {
            id = asset.Id,
            name = asset.Name,
            path = asset.FilePath,
            type = asset.Type.ToString(),
            durationSeconds = asset.Duration,
            width = asset.SourceWidth,
            height = asset.SourceHeight,
            fps = asset.SourceFps,
            hasAudio = asset.HasAudio,
            folderId = asset.FolderId,
            isGenerated = asset.IsGenerated,
            generationPrompt = asset.GenerationPrompt,
            usedByClips = clipRefs,
        };

        return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.DeleteMedia)]
    [Description("Remove a media asset from the project. Args: asset_id (string).")]
    public Task<string> DeleteMediaAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "asset_id", out var assetId) || string.IsNullOrWhiteSpace(assetId))
            return Task.FromResult(McpToolHelpers.Error("asset_id (string) is required"));

        if (!_assets.Catalog.TryGetById(assetId, out var asset) || asset == null)
            return Task.FromResult(McpToolHelpers.Error($"Asset '{assetId}' not found."));

        // Check if any clips reference this asset
        var tl = _store.State.Timeline.Timeline;
        var inUse = tl?.Tracks.SelectMany(t => t.Clips).Any(c => c.MediaRef == asset.FilePath) ?? false;

        if (inUse)
            return Task.FromResult(McpToolHelpers.Error($"Asset '{asset.Name}' is in use by clips on the timeline. Remove clips first."));

        _assets.RemoveAsset(_store.State.ProjectId, assetId);
        return Task.FromResult(McpToolHelpers.Ok(new { removed = assetId, name = asset.Name }));
    }

    [McpServerTool(Name = ToolDefinitions.ImportMedia)]
    [Description("Add a media file to the timeline as a new clip. Args: path (string, absolute file path), start_frame (int, default 0), track_index (int, optional — omit to create a new track).")]
    public async Task<string> ImportMediaAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
            return McpToolHelpers.Error("path (string) is required");

        if (!File.Exists(path))
            return McpToolHelpers.Error($"File not found: {path}");

        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var mediaType = ext switch
        {
            "mp4" or "mov" or "avi" or "mkv" or "webm" => ClipType.Video,
            "mp3" or "wav" or "aac" or "flac" or "ogg" => ClipType.Audio,
            "png" or "jpg" or "jpeg" or "bmp" or "webp" => ClipType.Image,
            _ => ClipType.Video,
        };

        McpToolHelpers.TryGetInt(args, "start_frame", out var startFrame);
        McpToolHelpers.TryGetInt(args, "track_index", out var trackIndex);

        if (startFrame < 0)
            return McpToolHelpers.Error("start_frame must be >= 0");

        var state = _store.State;
        var tl = state.Timeline.Timeline;

        // Determine track
        Track targetTrack;
        if (trackIndex >= 0 && trackIndex < tl.Tracks.Count)
        {
            targetTrack = tl.Tracks[trackIndex];
        }
        else
        {
            targetTrack = new Track
            {
                Name = mediaType == ClipType.Audio ? $"Audio {tl.Tracks.Count(t => t.Type == ClipType.Audio) + 1}" : $"Video {tl.Tracks.Count(t => t.Type == ClipType.Video) + 1}",
                Type = mediaType,
            };
        }

        // Use ImportAssetAsync for proper asset registration, then command for timeline mutation
        var asset = await _assets.ImportAssetAsync(state.ProjectId, path);

        // Find or create the track first via a command, then add clip
        var cmd = new AddClipsAsyncCommand(new[] { asset }, targetTrack.Id, startFrame);
        var result = await _queue.EnqueueAsync(cmd, ct);

        return result.Succeeded
            ? McpToolHelpers.Ok(new { path, startFrame, trackId = targetTrack.Id, mediaType = mediaType.ToString() })
            : McpToolHelpers.Error(result.ErrorMessage ?? "Failed to add clip to timeline");
    }
}
