using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
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
        if (!TryGetString(args, "source_clip_id", out var clipId) || string.IsNullOrEmpty(clipId))
            return Error("generate_captions requires source_clip_id (string)");

        var tl = _store.State.Timeline.Timeline;
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
            return Error($"Clip '{clipId}' not found in timeline.");

        int fps = tl.Fps > 0 ? tl.Fps : 30;
        var srtPath = Path.ChangeExtension(sourceClip.MediaRef, ".srt");
        List<SrtEntry> entries;

        if (File.Exists(srtPath))
        {
            entries = ParseSrt(await File.ReadAllTextAsync(srtPath, ct));
        }
        else
        {
            // Generate evenly-spaced placeholder captions
            entries = BuildPlaceholders(sourceClip.DurationFrames, fps);
        }

        if (entries.Count == 0)
            return Error("No caption entries found.");

        int addedCount = 0;
        foreach (var entry in entries)
        {
            int startFrame = sourceClip.StartFrame + (int)(entry.Start.TotalSeconds * fps);
            int endFrame   = sourceClip.StartFrame + (int)(entry.End.TotalSeconds * fps);
            int dur        = Math.Max(1, endFrame - startFrame);

            // Write caption text to a temp .srt file so asset manager can import it
            string tmpSrt = Path.Combine(
                Path.GetTempPath(),
                $"caption_{Guid.NewGuid():N}.srt");

            await File.WriteAllTextAsync(tmpSrt,
                $"1\n{FormatSrtTime(entry.Start)} --> {FormatSrtTime(entry.End)}\n{entry.Text}\n",
                ct);

            var asset = await _assets.ImportAssetAsync(_store.State.ProjectId, tmpSrt);
            asset.Name = entry.Text[..Math.Min(30, entry.Text.Length)];

            var cmd = new AddClipsAsyncCommand(new[] { asset }, "A2", startFrame);
            var result = await _queue.EnqueueAsync(cmd, ct);
            if (result.Succeeded) addedCount++;
        }

        return JsonSerializer.Serialize(new { ok = true, captionsAdded = addedCount });
    }

    // ── SRT parsing ─────────────────────────────────────────────────────────

    private sealed record SrtEntry(TimeSpan Start, TimeSpan End, string Text);

    private static List<SrtEntry> ParseSrt(string content)
    {
        var entries = new List<SrtEntry>();
        // Split on blank lines between blocks
        var blocks = Regex.Split(content.Trim(), @"\r?\n\r?\n");
        foreach (var block in blocks)
        {
            var lines = block.Trim().Split('\n');
            if (lines.Length < 3) continue;
            // lines[0] = index, lines[1] = timecode, lines[2..] = text
            var timeParts = lines[1].Split(" --> ");
            if (timeParts.Length != 2) continue;
            if (!TryParseSrtTime(timeParts[0].Trim(), out var start)) continue;
            if (!TryParseSrtTime(timeParts[1].Trim(), out var end)) continue;
            var text = string.Join(" ", lines[2..]).Trim();
            entries.Add(new SrtEntry(start, end, text));
        }
        return entries;
    }

    private static bool TryParseSrtTime(string s, out TimeSpan result)
    {
        result = default;
        // Format: HH:MM:SS,mmm
        var m = Regex.Match(s, @"(\d+):(\d+):(\d+)[,.](\d+)");
        if (!m.Success) return false;
        result = new TimeSpan(0,
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value),
            int.Parse(m.Groups[3].Value),
            int.Parse(m.Groups[4].Value.PadRight(3, '0')[..3]));
        return true;
    }

    private static string FormatSrtTime(TimeSpan t) =>
        $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2},{t.Milliseconds:D3}";

    private static List<SrtEntry> BuildPlaceholders(int totalFrames, int fps)
    {
        // 5-second subtitle intervals
        int intervalFrames = fps * 5;
        var entries = new List<SrtEntry>();
        for (int f = 0; f + intervalFrames <= totalFrames; f += intervalFrames)
        {
            var start = TimeSpan.FromSeconds((double)f / fps);
            var end   = TimeSpan.FromSeconds((double)(f + intervalFrames - 1) / fps);
            entries.Add(new SrtEntry(start, end, $"[Caption {entries.Count + 1}]"));
        }
        return entries;
    }

    private static bool TryGetString(JsonElement el, string key, out string? value)
    {
        value = null;
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
        { value = p.GetString(); return true; }
        return false;
    }

    private static string Error(string msg) =>
        JsonSerializer.Serialize(new { ok = false, error = msg });
}
