using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class TimelineTools
{
    private readonly EditorStore _store;
    private readonly CommandQueue _queue;

    public TimelineTools(EditorStore store, CommandQueue queue)
    {
        _store = store;
        _queue = queue;
    }

    [McpServerTool(Name = ToolDefinitions.InspectTimeline)]
    [Description("Return a JSON snapshot of the current timeline: tracks, clips, durations, fps.")]
    public Task<string> InspectTimelineAsync(CancellationToken ct = default)
    {
        var state = _store.State;
        var tl = state.Timeline.Timeline;
        var snapshot = new
        {
            fps    = tl.Fps,
            width  = tl.Width,
            height = tl.Height,
            totalFrames = tl.TotalFrames,
            durationSeconds = tl.Fps > 0 ? (double)tl.TotalFrames / tl.Fps : 0,
            tracks = tl.Tracks.Select((track, ti) => new
            {
                index = ti,
                id    = track.Id,
                name  = track.Name,
                clips = track.Clips.Select(c => new
                {
                    id              = c.Id,
                    mediaRef        = c.MediaRef,
                    type            = c.MediaType.ToString(),
                    startFrame      = c.StartFrame,
                    durationFrames  = c.DurationFrames,
                    endFrame        = c.EndFrame,
                    startSeconds    = tl.Fps > 0 ? (double)c.StartFrame / tl.Fps : 0,
                    durationSeconds = tl.Fps > 0 ? (double)c.DurationFrames / tl.Fps : 0,
                }),
            }),
        };
        return Task.FromResult(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.SplitClip)]
    [Description("Split a clip at a given frame, producing two clips in place. Args: clip_id (string), split_frame (int).")]
    public async Task<string> SplitClipAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetString(args, "clip_id", out var clipId) || !TryGetInt(args, "split_frame", out var frame))
            return Error("split_clip requires clip_id (string) and split_frame (int)");

        var cmd = new SplitClipAsyncCommand(clipId!, frame);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.TrimClip)]
    [Description("Adjust a clip's trim points. Args: clip_id (string), trim_start (int, source frames to skip at head, default 0), trim_end (int, source frames to skip at tail, default 0).")]
    public async Task<string> TrimClipAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetString(args, "clip_id", out var clipId))
            return Error("trim_clip requires clip_id (string)");

        TryGetInt(args, "trim_start", out var trimStart);
        TryGetInt(args, "trim_end", out var trimEnd);
        var cmd = new TrimClipAsyncCommand(clipId!, trimStart, trimEnd);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.MoveClip)]
    [Description("Move a clip to a different start frame and/or track. Args: clip_id (string), start_frame (int), track_id (string, optional — omit to keep current track).")]
    public async Task<string> MoveClipAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetString(args, "clip_id", out var clipId) || !TryGetInt(args, "start_frame", out var startFrame))
            return Error("move_clip requires clip_id (string) and start_frame (int)");

        // Resolve track_id: caller supplies it or we look it up from the current timeline
        TryGetString(args, "track_id", out var trackId);
        if (string.IsNullOrEmpty(trackId))
        {
            var tl = _store.State.Timeline.Timeline;
            trackId = tl.Tracks.SelectMany(t => t.Clips, (t, c) => (t, c))
                         .FirstOrDefault(p => p.c.Id == clipId).t?.Id ?? string.Empty;
        }
        if (string.IsNullOrEmpty(trackId))
            return Error($"Clip '{clipId}' not found in timeline.");

        var cmd = new MoveClipAsyncCommand(clipId!, startFrame, trackId);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.RemoveClips)]
    [Description("Remove one or more clips by ID. Args: clip_ids (array of strings).")]
    public async Task<string> RemoveClipsAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetStringArray(args, "clip_ids", out var ids) || ids is null || ids.Count == 0)
            return Error("remove_clips requires clip_ids (array of strings)");

        var cmd = new RemoveClipsAsyncCommand(ids);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.RippleDelete)]
    [Description("Remove a clip and shift downstream clips left to close the gap. Args: clip_id (string).")]
    public async Task<string> RippleDeleteAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetString(args, "clip_id", out var clipId))
            return Error("ripple_delete requires clip_id (string)");

        var cmd = new RippleDeleteAsyncCommand(new[] { clipId! });
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.ApplyEffect)]
    [Description("Apply an effect to a specific clip. Args: clip_id (string), effect_type (string, e.g. color_grade, glow, clarity, vignette).")]
    public async Task<string> ApplyEffectAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!TryGetString(args, "clip_id", out var clipId) || !TryGetString(args, "effect_type", out var effectType))
            return Error("apply_effect requires clip_id (string) and effect_type (string)");

        var cmd = new AddEffectAsyncCommand(clipId!, effectType!);
        return await EnqueueAsync(cmd, ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> EnqueueAsync(IAsyncCommand cmd, CancellationToken ct)
    {
        var result = await _queue.EnqueueAsync(cmd, ct);
        return result.Succeeded
            ? "{\"ok\":true}"
            : JsonSerializer.Serialize(new { ok = false, error = result.ErrorMessage });
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

    private static bool TryGetStringArray(JsonElement el, string key, out List<string>? value)
    {
        value = null;
        if (!el.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.Array) return false;
        value = p.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                  .Select(x => x.GetString()!).ToList();
        return true;
    }
}
