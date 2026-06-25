using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class AssetTools
{
    private readonly EditorStore _store;

    public AssetTools(EditorStore store) => _store = store;

    [McpServerTool(Name = ToolDefinitions.ListAssets)]
    [Description("List all media assets referenced by clips in the current project.")]
    public Task<string> ListAssetsAsync(CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        var assets = tl.Tracks
            .SelectMany(t => t.Clips)
            .GroupBy(c => c.MediaRef)
            .Select(g => new
            {
                mediaRef = g.Key,
                type     = g.First().MediaType.ToString(),
                usedByClips = g.Select(c => c.Id).ToList(),
            })
            .ToList();

        return Task.FromResult(JsonSerializer.Serialize(assets, new JsonSerializerOptions
        {
            WriteIndented = false,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.ImportMedia)]
    [Description("Add a media file to the timeline as a new clip. Args: path (string, absolute file path), start_frame (int, default 0), track_index (int, optional — omit to create a new track).")]
    public Task<string> ImportMediaAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetString(args, "path", out var path) || string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Error("import_media requires path (string)"));

        if (!File.Exists(path))
            return Task.FromResult(Error($"File not found: {path}"));

        TryGetInt(args, "start_frame", out var startFrame);
        TryGetInt(args, "track_index", out var trackIndex);

        // Determine media type from extension
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var mediaType = ext is "mp4" or "mov" or "avi" or "mkv" or "webm"
            ? ClipType.Video
            : ext is "mp3" or "wav" or "aac" or "flac" or "ogg"
            ? ClipType.Audio
            : ClipType.Video;

        _store.MutateTimeline("Import Media", tl =>
        {
            Track track;
            if (trackIndex >= 0 && trackIndex < tl.Tracks.Count)
            {
                track = tl.Tracks[trackIndex];
            }
            else
            {
                track = new Track { Name = Path.GetFileNameWithoutExtension(path) };
                tl.Tracks.Add(track);
            }

            var clip = new Clip
            {
                MediaRef       = path,
                MediaType      = mediaType,
                SourceClipType = mediaType,
                StartFrame     = startFrame,
                DurationFrames = 300, // placeholder; real duration requires media probe
            };
            track.Clips.Add(clip);
            track.Clips = track.Clips.OrderBy(c => c.StartFrame).ToList();
        });

        return Task.FromResult("{\"ok\":true}");
    }

    private static string Error(string msg) =>
        JsonSerializer.Serialize(new { ok = false, error = msg });

    private static bool TryGetString(JsonElement el, string key, out string? value)
    {
        value = null;
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
        { value = p.GetString(); return true; }
        return false;
    }

    private static bool TryGetInt(JsonElement el, string key, out int value)
    {
        value = 0;
        if (el.TryGetProperty(key, out var p) && p.TryGetInt32(out value)) return true;
        return false;
    }
}
