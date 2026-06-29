using System.Text.RegularExpressions;

namespace Lumos.Application;

/// An SRT subtitle entry with timing and text.
public sealed record SrtEntry(TimeSpan Start, TimeSpan End, string Text);

/// A filler-word region with timing and context for transcript analysis.
public sealed record FillerRegion(
    int StartFrame,
    int EndFrame,
    string FillerWord,
    string Context);

/// Shared SRT parsing and transcript utilities used by both
/// CaptionTools (MCP) and TranscriptCache (AI editing).
public static class SrtParser
{
    /// Common list of filler words for transcript analysis.
    public static readonly string[] FillerWords =
        ["um", "uh", "like", "you know", "actually", "basically", "literally", "sort of", "kind of"];

    /// Parse a standard SRT string into entries.
    public static List<SrtEntry> ParseSrt(string content)
    {
        var entries = new List<SrtEntry>();
        var blocks = Regex.Split(content.Trim(), @"\r?\n\r?\n");
        foreach (var block in blocks)
        {
            var lines = block.Trim().Split('\n');
            if (lines.Length < 3) continue;
            var timeParts = lines[1].Split(" --> ");
            if (timeParts.Length != 2) continue;
            if (!TryParseSrtTime(timeParts[0].Trim(), out var start)) continue;
            if (!TryParseSrtTime(timeParts[1].Trim(), out var end)) continue;
            var text = string.Join(" ", lines[2..]).Trim();
            entries.Add(new SrtEntry(start, end, text));
        }
        return entries;
    }

    /// Parse an SRT string into transcript segments with frame-based timing.
    public static List<TranscriptSegment> ParseSrtToSegments(string srtContent, int fps)
    {
        var entries = ParseSrt(srtContent);
        return entries.Select(e => new TranscriptSegment(
            (int)(e.Start.TotalSeconds * fps),
            (int)(e.End.TotalSeconds * fps),
            e.Text)).ToList();
    }

    public static bool TryParseSrtTime(string s, out TimeSpan result)
    {
        result = default;
        var m = Regex.Match(s, @"(\d+):(\d+):(\d+)[,.](\d+)");
        if (!m.Success) return false;
        result = new TimeSpan(0,
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value),
            int.Parse(m.Groups[3].Value),
            int.Parse(m.Groups[4].Value.PadRight(3, '0')[..3]));
        return true;
    }

    public static string FormatSrtTime(TimeSpan t) =>
        $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2},{t.Milliseconds:D3}";

    /// Build placeholder SRT entries for a given duration.
    public static List<SrtEntry> BuildPlaceholderEntries(int totalFrames, int fps, string label = "Caption")
    {
        int intervalFrames = fps * 5;
        var entries = new List<SrtEntry>();
        for (int f = 0; f + intervalFrames <= totalFrames; f += intervalFrames)
        {
            var start = TimeSpan.FromSeconds((double)f / fps);
            var end = TimeSpan.FromSeconds((double)(f + intervalFrames - 1) / fps);
            entries.Add(new SrtEntry(start, end, $"[{label} {entries.Count + 1}]"));
        }
        return entries;
    }

    /// Build placeholder transcript segments for a given duration.
    public static List<TranscriptSegment> BuildPlaceholderSegments(int totalFrames, int fps, string label = "Segment")
    {
        int intervalFrames = fps * 5;
        var segments = new List<TranscriptSegment>();
        for (int f = 0; f + intervalFrames <= totalFrames; f += intervalFrames)
        {
            int end = Math.Min(f + intervalFrames - 1, totalFrames);
            segments.Add(new TranscriptSegment(f, end, $"[{label} {segments.Count + 1}]"));
        }
        return segments;
    }

    /// Check if text contains any filler word, returning the matched word or null.
    public static string? FindFillerInText(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var filler in FillerWords)
        {
            if (lower.Contains(filler))
                return filler;
        }
        return null;
    }
}
