using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;
using Lumos.Media;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class AIEditingTools
{
    private readonly EditorStore _store;
    private readonly CommandQueue _queue;
    private readonly TranscriptCache _transcriptCache;
    private readonly AgentActionHistory _actionHistory;
    private readonly VideoEngine _videoEngine;

    public AIEditingTools(
        EditorStore store,
        CommandQueue queue,
        TranscriptCache transcriptCache,
        AgentActionHistory actionHistory,
        VideoEngine videoEngine)
    {
        _store = store;
        _queue = queue;
        _transcriptCache = transcriptCache;
        _actionHistory = actionHistory;
        _videoEngine = videoEngine;
    }

    // ── Transcript tools ─────────────────────────────────────────────────

    [McpServerTool(Name = "get_transcript")]
    [Description("Get the transcript for a clip or track. Args: clip_id (string, optional), track_id (string, optional). Returns transcript segments with timing and text.")]
    public Task<string> GetTranscriptAsync(JsonElement args, CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        McpToolHelpers.TryGetString(args, "clip_id", out var clipId);
        McpToolHelpers.TryGetString(args, "track_id", out var trackId);

        var allSegments = new List<object>();

        foreach (var track in tl.Tracks)
        {
            if (!string.IsNullOrEmpty(trackId) && track.Id != trackId) continue;

            foreach (var clip in track.Clips)
            {
                if (!string.IsNullOrEmpty(clipId) && clip.Id != clipId) continue;

                var segments = _transcriptCache.GetTranscript(clip.MediaRef);
                if (segments == null) continue;

                foreach (var seg in segments)
                {
                    allSegments.Add(new
                    {
                        clipId = clip.Id,
                        clipName = Path.GetFileNameWithoutExtension(clip.MediaRef),
                        globalStartFrame = clip.StartFrame + seg.StartFrame,
                        globalEndFrame = clip.StartFrame + seg.EndFrame,
                        text = seg.Text,
                        confidence = seg.Confidence,
                    });
                }
            }
        }

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            segmentCount = allSegments.Count,
            segments = allSegments,
        }, new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
    }

    [McpServerTool(Name = "search_transcript")]
    [Description("Search transcript text for a query across all clips. Args: query (string). Returns matching segments with clip and timing info.")]
    public Task<string> SearchTranscriptAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "query", out var query) || string.IsNullOrWhiteSpace(query))
            return Task.FromResult(McpToolHelpers.Error("query (string) is required"));

        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        var results = new List<object>();

        foreach (var track in tl.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                var matches = _transcriptCache.SearchTranscript(clip.MediaRef, query, clip.StartFrame);
                foreach (var (seg, baseFrame) in matches)
                {
                    results.Add(new
                    {
                        clipId = clip.Id,
                        clipName = Path.GetFileNameWithoutExtension(clip.MediaRef),
                        startFrame = baseFrame + seg.StartFrame,
                        endFrame = baseFrame + seg.EndFrame,
                        text = seg.Text,
                    });
                }
            }
        }

        RecordAction("search_transcript", $"Searched transcript for \"{query}\" — found {results.Count} matches", true);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            query,
            matchCount = results.Count,
            results,
        }, new JsonSerializerOptions { WriteIndented = false }));
    }

    // ── Filler word tools ────────────────────────────────────────────────

    [McpServerTool(Name = "detect_filler_words")]
    [Description("Detect filler words (um, uh, like, etc.) in all clips' transcripts. Returns regions with filler word and context.")]
    public Task<string> DetectFillerWordsAsync(CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        var regions = HighlightDetector.DetectFillerRegions(tl, _transcriptCache);

        RecordAction("detect_filler_words", $"Detected {regions.Count} filler word regions", true);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            fillerCount = regions.Count,
            regions = regions.Select(r => new
            {
                startFrame = r.StartFrame,
                endFrame = r.EndFrame,
                fillerWord = r.FillerWord,
                context = r.Context,
            }),
        }, new JsonSerializerOptions { WriteIndented = false }));
    }

    [McpServerTool(Name = "remove_filler_regions")]
    [Description("Remove regions containing filler words from clips by splitting and deleting. Args: clip_id (string, optional — omit for all clips).")]
    public async Task<string> RemoveFillerRegionsAsync(JsonElement args, CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return McpToolHelpers.Error("No timeline loaded.");

        McpToolHelpers.TryGetString(args, "clip_id", out var clipId);

        var regions = HighlightDetector.DetectFillerRegions(tl, _transcriptCache);
        if (!string.IsNullOrEmpty(clipId))
            regions = regions.Where(r => tl.Tracks.SelectMany(t => t.Clips).Any(c => c.Id == clipId && c.StartFrame <= r.StartFrame && c.EndFrame >= r.EndFrame)).ToList();

        if (regions.Count == 0)
            return McpToolHelpers.Ok(new { removedCount = 0 });

        int removedCount = 0;
        foreach (var (regionStart, regionEnd, filler, context) in regions)
        {
            int regionDur = regionEnd - regionStart;
            if (regionDur < 5) continue;

            // Find the clip that contains this filler region
            foreach (var track in tl.Tracks)
            {
                var clip = track.Clips.FirstOrDefault(c => c.StartFrame < regionEnd && c.EndFrame > regionStart);
                if (clip == null) continue;

                int localStart = Math.Max(clip.StartFrame, regionStart);
                int localEnd = Math.Min(clip.EndFrame, regionEnd);

                // Process splits from right to left so frame references stay valid
                bool splitRight = localEnd < clip.EndFrame - 2;
                bool splitLeft = localStart > clip.StartFrame + 2;

                if (splitRight)
                {
                    var splitCmd = new SplitClipAsyncCommand(clip.Id, localEnd);
                    await _queue.EnqueueAsync(splitCmd, ct);
                }
                if (splitLeft)
                {
                    var splitCmd2 = new SplitClipAsyncCommand(clip.Id, localStart);
                    await _queue.EnqueueAsync(splitCmd2, ct);
                }

                // After splits, find the filler clip by exact StartFrame match
                // The left-split creates a right-side clip whose StartFrame == localStart
                // The right-split creates a left-side clip whose StartFrame == clip.StartFrame (unchanged)
                // If both splits: the middle clip has StartFrame == localStart
                // If left split only: right clip has StartFrame == localStart
                // If right split only: left clip (the filler side) keeps original StartFrame
                string? targetClipId = null;
                var afterSplit = track.Clips.ToList();

                if (splitLeft && splitRight)
                {
                    // Middle clip after two splits — StartFrame == localStart
                    targetClipId = afterSplit.FirstOrDefault(c => c.StartFrame == localStart)?.Id;
                }
                else if (splitLeft && !splitRight)
                {
                    // Only left split — the right-side clip starts at localStart
                    targetClipId = afterSplit.FirstOrDefault(c => c.StartFrame == localStart)?.Id;
                }
                else if (!splitLeft && splitRight)
                {
                    // Only right split — the left-side clip keeps original StartFrame
                    targetClipId = afterSplit.FirstOrDefault(c => c.StartFrame == clip.StartFrame)?.Id;
                }
                else
                {
                    // No split — the filler covers the whole clip
                    targetClipId = clip.Id;
                }

                if (targetClipId != null)
                {
                    var removeCmd = new RemoveClipsAsyncCommand(new[] { targetClipId });
                    var result = await _queue.EnqueueAsync(removeCmd, ct);
                    if (result.Succeeded) removedCount++;
                }
            }
        }

        RecordAction("remove_filler_regions", $"Removed {removedCount} filler word regions", true);
        return McpToolHelpers.Ok(new { removedCount });
    }

    // ── Highlight tools ──────────────────────────────────────────────────

    [McpServerTool(Name = "detect_highlights")]
    [Description("Detect highlight-worthy regions based on transcript keywords, scene changes, and clip metadata. Args: top_count (int, default 5).")]
    public Task<string> DetectHighlightsAsync(JsonElement args, CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        McpToolHelpers.TryGetInt(args, "top_count", out var topCount);
        if (topCount <= 0) topCount = 5;

        var highlights = HighlightDetector.DetectHighlights(tl, _transcriptCache, topCount);

        RecordAction("detect_highlights", $"Detected {highlights.Count} highlight regions", true);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            highlightCount = highlights.Count,
            highlights = highlights.Select(h => new
            {
                startFrame = h.StartFrame,
                endFrame = h.EndFrame,
                score = h.Score,
                label = h.Label,
            }),
        }, new JsonSerializerOptions { WriteIndented = false }));
    }

    // ── Frame inspection tool ────────────────────────────────────────────

    [McpServerTool(Name = "inspect_frame")]
    [Description("Get information about a specific frame in the timeline for agent verification. Args: frame (int), width (int, optional, default 320), height (int, optional, default 180). Returns a description of frame contents.")]
    public async Task<string> InspectFrameAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetInt(args, "frame", out var frame))
            return McpToolHelpers.Error("frame (int) is required");

        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return McpToolHelpers.Error("No timeline loaded.");

        if (frame < 0 || frame >= tl.TotalFrames)
            return McpToolHelpers.Error($"frame ({frame}) is out of range (0-{tl.TotalFrames})");

        McpToolHelpers.TryGetInt(args, "width", out var width);
        McpToolHelpers.TryGetInt(args, "height", out var height);
        if (width <= 0) width = 320;
        if (height <= 0) height = 180;

        // Get pixel data for analysis
        var pixelData = await _videoEngine.GetCompositedFrameAsync(frame, width, height);
        if (pixelData == null)
            return McpToolHelpers.Error("Failed to render frame.");

        // Analyze frame content
        float avgLuminance = 0;
        float avgRed = 0, avgGreen = 0, avgBlue = 0;
        int totalPixels = pixelData.Length / 4;

        for (int i = 0; i < pixelData.Length; i += 4)
        {
            float b = pixelData[i] / 255f;
            float g = pixelData[i + 1] / 255f;
            float r = pixelData[i + 2] / 255f;
            avgBlue += b;
            avgGreen += g;
            avgRed += r;
            avgLuminance += 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        avgLuminance /= totalPixels;
        avgRed /= totalPixels;
        avgGreen /= totalPixels;
        avgBlue /= totalPixels;

        // Find clips at this frame
        var clipsAtFrame = new List<object>();
        foreach (var track in tl.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.Contains(frame))
                {
                    clipsAtFrame.Add(new
                    {
                        clipId = clip.Id,
                        clipName = Path.GetFileNameWithoutExtension(clip.MediaRef),
                        type = clip.MediaType.ToString(),
                        localFrame = frame - clip.StartFrame,
                        opacity = clip.OpacityAt(frame),
                        transform = new { x = clip.Transform.CenterX, y = clip.Transform.CenterY, w = clip.Transform.Width, h = clip.Transform.Height },
                    });
                }
            }
        }

        // Determine scene description
        string sceneType = avgLuminance < 0.15f ? "dark scene" :
                           avgLuminance > 0.85f ? "bright scene" : "normal exposure";

        string dominantColor = avgRed > avgGreen && avgRed > avgBlue ? "red/warm" :
                               avgGreen > avgRed && avgGreen > avgBlue ? "green" :
                               avgBlue > avgRed && avgBlue > avgGreen ? "blue/cool" : "neutral";

        RecordAction("inspect_frame", $"Inspected frame {frame} — {sceneType}, {dominantColor}, {clipsAtFrame.Count} clips visible", true);

        return JsonSerializer.Serialize(new
        {
            frame,
            totalFrames = tl.TotalFrames,
            sceneType,
            dominantColor,
            averageLuminance = Math.Round(avgLuminance, 3),
            averageColor = new { r = Math.Round(avgRed, 3), g = Math.Round(avgGreen, 3), b = Math.Round(avgBlue, 3) },
            clipsAtFrame,
        }, new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
    }

    // ── Action history tool ──────────────────────────────────────────────

    [McpServerTool(Name = "get_action_history")]
    [Description("Get the recent agent action history for verification. Args: count (int, default 10). Returns recent actions with tool name, description, timestamp, and success status.")]
    public Task<string> GetActionHistoryAsync(JsonElement args, CancellationToken ct = default)
    {
        McpToolHelpers.TryGetInt(args, "count", out var count);
        if (count <= 0) count = 10;

        var actions = _actionHistory.GetRecent(count);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            actionCount = actions.Count,
            actions = actions.Select(a => new
            {
                id = a.Id,
                tool = a.ToolName,
                description = a.Description,
                timestamp = a.Timestamp,
                succeeded = a.Succeeded,
            }),
        }, new JsonSerializerOptions { WriteIndented = false }));
    }

    // ── Silence analysis tool ───────────────────────────────────────────

    [McpServerTool(Name = "analyze_silences")]
    [Description("Analyze audio tracks for silence regions. Args: threshold_db (double, default -40), min_duration_frames (int, default 15). Returns silent regions with timing.")]
    public Task<string> AnalyzeSilencesAsync(JsonElement args, CancellationToken ct = default)
    {
        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        McpToolHelpers.TryGetDouble(args, "threshold_db", out var thresholdDb);
        McpToolHelpers.TryGetInt(args, "min_duration_frames", out var minDurationFrames);
        if (thresholdDb == 0) thresholdDb = -40.0;
        if (minDurationFrames <= 0) minDurationFrames = 15;

        var allSilences = new List<object>();

        foreach (var track in tl.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.MediaType != ClipType.Audio && clip.MediaType != ClipType.Video) continue;

                var silences = AudioAnalyzer.DetectSilences(clip.MediaRef, thresholdDb, minDurationFrames, tl.Fps);
                foreach (var (start, end) in silences)
                {
                    int globalStart = clip.StartFrame + start;
                    int globalEnd = clip.StartFrame + end;
                    allSilences.Add(new
                    {
                        clipId = clip.Id,
                        clipName = Path.GetFileNameWithoutExtension(clip.MediaRef),
                        globalStartFrame = globalStart,
                        globalEndFrame = globalEnd,
                        durationFrames = globalEnd - globalStart,
                        trackId = track.Id,
                    });
                }
            }
        }

        RecordAction("analyze_silences", $"Analyzed {allSilences.Count} silent regions", true);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            silenceCount = allSilences.Count,
            silences = allSilences,
        }, new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }));
    }

    // ── Helper ───────────────────────────────────────────────────────────

    private void RecordAction(string toolName, string description, bool succeeded, string? details = null)
    {
        _actionHistory.Record(toolName, description, succeeded, details);
    }
}
