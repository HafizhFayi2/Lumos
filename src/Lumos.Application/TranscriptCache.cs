using System.Collections.Concurrent;

namespace Lumos.Application;

/// A single transcribed segment with timing and text.
public sealed record TranscriptSegment(int StartFrame, int EndFrame, string Text, double Confidence = 1.0);

/// In-memory cache of transcripts for media assets.
/// In a production app, transcripts would be persisted alongside the project.
public sealed class TranscriptCache
{
    private readonly ConcurrentDictionary<string, List<TranscriptSegment>> _transcripts = new();

    public bool HasTranscript(string assetPath) => _transcripts.ContainsKey(assetPath);

    public List<TranscriptSegment>? GetTranscript(string assetPath)
    {
        _transcripts.TryGetValue(assetPath, out var segments);
        return segments;
    }

    public void SetTranscript(string assetPath, List<TranscriptSegment> segments)
    {
        _transcripts[assetPath] = segments;
    }

    /// Build a placeholder transcript from clip duration (when no SRT file exists).
    /// Delegates to shared SrtParser.
    public List<TranscriptSegment> BuildPlaceholders(int totalFrames, int fps, string label = "Segment")
        => SrtParser.BuildPlaceholderSegments(totalFrames, fps, label);

    /// Parse a standard SRT string into transcript segments.
    /// Delegates to shared SrtParser.
    public List<TranscriptSegment> ParseSrt(string srtContent, int fps)
        => SrtParser.ParseSrtToSegments(srtContent, fps);

    /// Search transcript for a text query within a frame range.
    public List<(TranscriptSegment Segment, int ClipStartFrame)> SearchTranscript(
        string assetPath, string query, int? clipStartFrame = null)
    {
        var result = new List<(TranscriptSegment, int)>();
        var segments = GetTranscript(assetPath);
        if (segments == null) return result;

        int baseFrame = clipStartFrame ?? 0;
        foreach (var seg in segments)
        {
            if (seg.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                result.Add((seg, baseFrame));
            }
        }
        return result;
    }

    /// Find filler-word segments in a transcript using shared SrtParser.
    public List<(TranscriptSegment Segment, string FillerWord)> FindFillerWords(string assetPath)
    {
        var result = new List<(TranscriptSegment, string)>();
        var segments = GetTranscript(assetPath);
        if (segments == null) return result;

        foreach (var seg in segments)
        {
            var filler = SrtParser.FindFillerInText(seg.Text);
            if (filler != null)
            {
                result.Add((seg, filler));
            }
        }
        return result;
    }
}
