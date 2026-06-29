using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.Assets;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class CaptionTools
{
    private readonly EditorStore _store;
    private readonly CommandQueue _queue;
    private readonly AssetManager _assets;

    public CaptionTools(EditorStore store, CommandQueue queue, AssetManager assets)
    {
        _store  = store;
        _queue  = queue;
        _assets = assets;
    }

    [McpServerTool(Name = ToolDefinitions.GenerateCaptions)]
    [Description("Parse an SRT file alongside a video clip and add subtitle clips to track A2. Args: source_clip_id (string).")]
    public async Task<string> GenerateCaptionsAsync(JsonElement args, CancellationToken ct = default)
    {
        var err = McpToolHelpers.ValidateClipId(args, out var clipId);
        if (err != null) return McpToolHelpers.Error(err);

        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return McpToolHelpers.Error("No timeline loaded.");

        Clip? sourceClip = null;
        foreach (var track in tl.Tracks)
        {
            foreach (var c in track.Clips)
            {
                if (c.Id == clipId) { sourceClip = c; break; }
            }
            if (sourceClip != null) break;
        }

        if (sourceClip == null)
            return McpToolHelpers.Error($"Clip '{clipId}' not found in timeline.");

        int fps = tl.Fps > 0 ? tl.Fps : 30;
        var srtPath = Path.ChangeExtension(sourceClip.MediaRef, ".srt");
        List<Lumos.Application.SrtEntry> entries;

        if (File.Exists(srtPath))
        {
            entries = SrtParser.ParseSrt(await File.ReadAllTextAsync(srtPath, ct));
        }
        else
        {
            entries = SrtParser.BuildPlaceholderEntries(sourceClip.DurationFrames, fps);
        }

        if (entries.Count == 0)
            return McpToolHelpers.Error("No caption entries found.");

        int addedCount = 0;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            int startFrame = sourceClip.StartFrame + (int)(entry.Start.TotalSeconds * fps);
            int endFrame   = sourceClip.StartFrame + (int)(entry.End.TotalSeconds * fps);
            int dur        = Math.Max(1, endFrame - startFrame);

            string tmpSrt = Path.Combine(
                Path.GetTempPath(),
                $"caption_{Guid.NewGuid():N}.srt");

            await File.WriteAllTextAsync(tmpSrt,
                $"1\n{SrtParser.FormatSrtTime(entry.Start)} --> {SrtParser.FormatSrtTime(entry.End)}\n{entry.Text}\n",
                ct);

            var asset = await _assets.ImportAssetAsync(_store.State.ProjectId, tmpSrt);
            asset.Name = entry.Text[..Math.Min(30, entry.Text.Length)];

            var cmd = new AddClipsAsyncCommand(new[] { asset }, "A2", startFrame);
            var result = await _queue.EnqueueAsync(cmd, ct);
            if (result.Succeeded) addedCount++;
        }

        return McpToolHelpers.Ok(new
        {
            captionsAdded = addedCount,
            sourceClipId = clipId,
            srtFound = File.Exists(srtPath),
        });
    }
}
