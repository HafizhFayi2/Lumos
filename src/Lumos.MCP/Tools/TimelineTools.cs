using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Lumos.Application;
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
    [Description("Return a detailed JSON snapshot of the current timeline: tracks, clips, durations, fps.")]
    public Task<string> InspectTimelineAsync(CancellationToken ct = default)
    {
        var state = _store.State;
        var tl = state.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

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
                isMuted  = track.IsMuted,
                isHidden = track.IsHidden,
                isLocked = track.IsSyncLocked,
                clips = track.Clips.Select(c => new
                {
                    id              = c.Id,
                    mediaRef        = c.MediaRef,
                    type            = c.MediaType.ToString(),
                    startFrame      = c.StartFrame,
                    durationFrames  = c.DurationFrames,
                    endFrame        = c.EndFrame,
                    speed           = c.Speed,
                    opacity         = c.Opacity,
                    effects         = c.Effects.Select(e => e.Type).ToList(),
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

    [McpServerTool(Name = ToolDefinitions.GetTimeline)]
    [Description("Return a concise summary of the current timeline: fps, resolution, total frames, track count, clip count.")]
    public Task<string> GetTimelineAsync(CancellationToken ct = default)
    {
        var state = _store.State;
        var tl = state.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        int totalClips = tl.Tracks.Sum(t => t.Clips.Count);
        var summary = new
        {
            fps = tl.Fps,
            width = tl.Width,
            height = tl.Height,
            totalFrames = tl.TotalFrames,
            durationSeconds = tl.Fps > 0 ? (double)tl.TotalFrames / tl.Fps : 0,
            trackCount = tl.Tracks.Count,
            clipCount = totalClips,
            isDirty = state.IsDirty,
            projectName = state.ProjectName,
        };
        return Task.FromResult(JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        }));
    }

    [McpServerTool(Name = ToolDefinitions.SplitClip)]
    [Description("Split a clip at a given frame, producing two clips in place. Args: clip_id (string), split_frame (int).")]
    public async Task<string> SplitClipAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        if (err != null) return McpToolHelpers.Error(err);

        if (!McpToolHelpers.TryGetInt(args, "split_frame", out var frame))
            return McpToolHelpers.Error("split_frame (int) is required");

        var clip = FindClip(clipId!);
        if (clip == null)
            return McpToolHelpers.Error($"Clip '{clipId}' not found in timeline.");

        if (frame <= clip.StartFrame || frame >= clip.EndFrame)
            return McpToolHelpers.Error($"split_frame ({frame}) must be between clip start ({clip.StartFrame}) and end ({clip.EndFrame}).");

        var cmd = new SplitClipAsyncCommand(clipId!, frame);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.TrimClip)]
    [Description("Adjust a clip's trim points. Args: clip_id (string), trim_start (int, source frames to skip at head, default 0), trim_end (int, source frames to skip at tail, default 0).")]
    public async Task<string> TrimClipAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        if (err != null) return McpToolHelpers.Error(err);

        McpToolHelpers.TryGetInt(args, "trim_start", out var trimStart);
        McpToolHelpers.TryGetInt(args, "trim_end", out var trimEnd);

        if (trimStart < 0)
            return McpToolHelpers.Error("trim_start must be >= 0");
        if (trimEnd < 0)
            return McpToolHelpers.Error("trim_end must be >= 0");

        var clip = FindClip(clipId!);
        if (clip == null)
            return McpToolHelpers.Error($"Clip '{clipId}' not found in timeline.");

        if (trimStart + trimEnd >= clip.DurationFrames)
            return McpToolHelpers.Error($"trim_start ({trimStart}) + trim_end ({trimEnd}) must be less than clip duration ({clip.DurationFrames}).");

        var cmd = new TrimClipAsyncCommand(clipId!, trimStart, trimEnd);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.MoveClip)]
    [Description("Move a clip to a different start frame and/or track. Args: clip_id (string), start_frame (int), track_id (string, optional — omit to keep current track).")]
    public async Task<string> MoveClipAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        if (err != null) return McpToolHelpers.Error(err);

        if (!McpToolHelpers.TryGetInt(args, "start_frame", out var startFrame))
            return McpToolHelpers.Error("start_frame (int) is required");

        if (startFrame < 0)
            return McpToolHelpers.Error("start_frame must be >= 0");

        // Resolve track_id: caller supplies it or we look it up from the current timeline
        McpToolHelpers.TryGetString(args, "track_id", out var trackId);
        if (string.IsNullOrEmpty(trackId))
        {
            var tl = _store.State.Timeline.Timeline;
            trackId = tl.Tracks.SelectMany(t => t.Clips, (t, c) => (t, c))
                         .FirstOrDefault(p => p.c.Id == clipId).t?.Id ?? string.Empty;
        }
        if (string.IsNullOrEmpty(trackId))
            return McpToolHelpers.Error($"Clip '{clipId}' not found in timeline.");

        var cmd = new MoveClipAsyncCommand(clipId!, startFrame, trackId);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.RemoveClips)]
    [Description("Remove one or more clips by ID. Args: clip_ids (array of strings).")]
    public async Task<string> RemoveClipsAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var ids);
        if (err != null) return McpToolHelpers.Error(err);

        // Validate all clip IDs exist
        var tl = _store.State.Timeline.Timeline;
        var allClipIds = tl.Tracks.SelectMany(t => t.Clips).Select(c => c.Id).ToHashSet();
        var missing = ids!.Where(id => !allClipIds.Contains(id)).ToList();
        if (missing.Count > 0)
            return McpToolHelpers.Error($"Clip(s) not found: {string.Join(", ", missing)}");

        var cmd = new RemoveClipsAsyncCommand(ids!);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.RippleDelete)]
    [Description("Remove a clip and shift downstream clips left to close the gap. Args: clip_id (string).")]
    public async Task<string> RippleDeleteAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        if (err != null) return McpToolHelpers.Error(err);

        var clip = FindClip(clipId!);
        if (clip == null)
            return McpToolHelpers.Error($"Clip '{clipId}' not found in timeline.");

        var cmd = new RippleDeleteAsyncCommand(new[] { clipId! });
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.ApplyEffect)]
    [Description("Apply an effect to a specific clip. Args: clip_id (string), effect_type (string, e.g. color_grade, glow, clarity, vignette).")]
    public async Task<string> ApplyEffectAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        if (err != null) return McpToolHelpers.Error(err);

        if (!McpToolHelpers.TryGetString(args, "effect_type", out var effectType) || string.IsNullOrWhiteSpace(effectType))
            return McpToolHelpers.Error("effect_type (string) is required");

        var effectErr = McpToolHelpers.ValidateEffectType(effectType!);
        if (effectErr != null) return McpToolHelpers.Error(effectErr);

        var clip = FindClip(clipId!);
        if (clip == null)
            return McpToolHelpers.Error($"Clip '{clipId}' not found in timeline.");

        var cmd = new AddEffectAsyncCommand(clipId!, effectType!);
        return await EnqueueAsync(cmd, ct);
    }

    [McpServerTool(Name = ToolDefinitions.AddClips)]
    [Description("Add clips to a track by clip ID. Args: clip_ids (array of strings), track_id (string), start_frame (int).")]
    public async Task<string> AddClipsAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "track_id", out var trackId) || string.IsNullOrWhiteSpace(trackId))
            return McpToolHelpers.Error("track_id (string) is required");

        if (!McpToolHelpers.TryGetInt(args, "start_frame", out var startFrame))
            return McpToolHelpers.Error("start_frame (int) is required");

        if (startFrame < 0)
            return McpToolHelpers.Error("start_frame must be >= 0");

        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var clipIds);
        if (err != null) return McpToolHelpers.Error(err);

        var assets = new List<Domain.Asset>();
        foreach (var clipId in clipIds!)
        {
            var clip = FindClip(clipId);
            if (clip == null)
                return McpToolHelpers.Error($"No clip found with id '{clipId}'.");

            assets.Add(new Domain.Asset
            {
                Id = clip.Id,
                FilePath = clip.MediaRef,
                Name = Path.GetFileName(clip.MediaRef),
                Type = clip.MediaType,
            });
        }

        var cmd = new AddClipsAsyncCommand(assets, trackId, startFrame);
        var result = await _queue.EnqueueAsync(cmd, ct);
        return result.Succeeded
            ? McpToolHelpers.Ok(new { addedCount = assets.Count, trackId, startFrame })
            : McpToolHelpers.Error(result.ErrorMessage ?? "Failed to add clips");
    }

    [McpServerTool(Name = ToolDefinitions.InsertClips)]
    [Description("Insert clips at a frame position, pushing existing clips right with ripple. Args: clip_ids (array of strings), track_id (string), insert_frame (int).")]
    public async Task<string> InsertClipsAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "track_id", out var trackId) || string.IsNullOrWhiteSpace(trackId))
            return McpToolHelpers.Error("track_id (string) is required");

        if (!McpToolHelpers.TryGetInt(args, "insert_frame", out var insertFrame))
            return McpToolHelpers.Error("insert_frame (int) is required");

        if (insertFrame < 0)
            return McpToolHelpers.Error("insert_frame must be >= 0");

        var err = McpToolHelpers.ValidateNonEmptyClipIds(args, out var clipIds);
        if (err != null) return McpToolHelpers.Error(err);

        var tl = _store.State.Timeline.Timeline;
        var targetTrack = tl.Tracks.FirstOrDefault(t => t.Id == trackId);
        if (targetTrack == null)
            return McpToolHelpers.Error($"Track '{trackId}' not found.");

        // Calculate total duration of new clips
        int pushAmount = 0;
        var assets = new List<Domain.Asset>();
        foreach (var clipId in clipIds!)
        {
            var clip = FindClip(clipId);
            if (clip == null)
                return McpToolHelpers.Error($"No clip found with id '{clipId}'.");
            pushAmount += clip.DurationFrames;
            assets.Add(new Domain.Asset
            {
                Id = clip.Id,
                FilePath = clip.MediaRef,
                Name = Path.GetFileName(clip.MediaRef),
                Type = clip.MediaType,
            });
        }

        // Push existing clips right
        var shifts = RippleEngine.ComputeRipplePush(targetTrack.Clips, insertFrame, pushAmount);
        _store.MutateTimeline("Ripple push for insert", t =>
        {
            foreach (var shift in shifts)
            {
                var c = t.Tracks.SelectMany(tr => tr.Clips).FirstOrDefault(c => c.Id == shift.ClipId);
                if (c != null) c.StartFrame = shift.NewStartFrame;
            }
        });

        // Add the new clips
        var cmd = new AddClipsAsyncCommand(assets, trackId, insertFrame);
        var result = await _queue.EnqueueAsync(cmd, ct);
        return result.Succeeded
            ? McpToolHelpers.Ok(new { insertedCount = assets.Count, trackId, insertFrame, pushedClips = shifts.Count })
            : McpToolHelpers.Error(result.ErrorMessage ?? "Failed to insert clips");
    }

    [McpServerTool(Name = ToolDefinitions.RippleDeleteRanges)]
    [Description("Remove all clips overlapping a frame range and shift remaining clips left to close gaps. Args: track_id (string), start_frame (int), end_frame (int).")]
    public Task<string> RippleDeleteRangesAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "track_id", out var trackId) || string.IsNullOrWhiteSpace(trackId))
            return Task.FromResult(McpToolHelpers.Error("track_id (string) is required"));

        if (!McpToolHelpers.TryGetInt(args, "start_frame", out var startFrame))
            return Task.FromResult(McpToolHelpers.Error("start_frame (int) is required"));

        if (!McpToolHelpers.TryGetInt(args, "end_frame", out var endFrame))
            return Task.FromResult(McpToolHelpers.Error("end_frame (int) is required"));

        if (startFrame < 0 || endFrame <= startFrame)
            return Task.FromResult(McpToolHelpers.Error("end_frame must be greater than start_frame, and both must be >= 0"));

        var tl = _store.State.Timeline.Timeline;
        var targetTrack = tl.Tracks.FirstOrDefault(t => t.Id == trackId);
        if (targetTrack == null)
            return Task.FromResult(McpToolHelpers.Error($"Track '{trackId}' not found."));

        // Find clips overlapping the range
        var overlapping = targetTrack.Clips
            .Where(c => c.StartFrame < endFrame && c.EndFrame > startFrame)
            .ToList();

        if (overlapping.Count == 0)
            return Task.FromResult(McpToolHelpers.Ok(new { removedCount = 0, shiftedCount = 0 }));

        ct.ThrowIfCancellationRequested();

        var removedIds = overlapping.Select(c => c.Id).ToHashSet();
        var removedRanges = overlapping.Select(c => new FrameRange(c.StartFrame, c.EndFrame)).ToList();

        // Compute ripple shifts for remaining clips on this track
        var shifts = RippleEngine.ComputeRippleShiftsForRanges(targetTrack.Clips, removedRanges);

        _store.MutateTimeline("Ripple delete ranges", t =>
        {
            // Remove overlapping clips
            foreach (var id in removedIds)
            {
                var track = t.Tracks.FirstOrDefault(tr => tr.Id == trackId);
                track?.Clips.RemoveAll(c => c.Id == id);
            }

            // Apply shifts to remaining clips
            foreach (var shift in shifts)
            {
                var c = t.Tracks.SelectMany(tr => tr.Clips).FirstOrDefault(c => c.Id == shift.ClipId);
                if (c != null) c.StartFrame = shift.NewStartFrame;
            }
        });

        return Task.FromResult(McpToolHelpers.Ok(new { removedCount = removedIds.Count, shiftedCount = shifts.Count }));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> EnqueueAsync(IAsyncCommand cmd, CancellationToken ct)
    {
        var result = await _queue.EnqueueAsync(cmd, ct);
        return result.Succeeded
            ? McpToolHelpers.Ok()
            : McpToolHelpers.Error(result.ErrorMessage ?? "Command execution failed");
    }

    private Domain.Clip? FindClip(string clipId)
    {
        var tl = _store.State.Timeline.Timeline;
        return tl.Tracks.SelectMany(t => t.Clips).FirstOrDefault(c => c.Id == clipId);
    }
}
