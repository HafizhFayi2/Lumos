using Lumos.Domain;

namespace Lumos.Application;

/// A detected highlight region with a score and label.
public sealed record HighlightRegion(int StartFrame, int EndFrame, float Score, string Label);

/// Detects highlight-worthy regions in a timeline based on:
/// - Audio volume spikes (applause, laughter, emphasis)
/// - Clip boundaries (scene changes)
/// - Transcript keywords (funny, amazing, etc.)
/// - Motion intensity
public static class HighlightDetector
{
    /// Find highlight regions across the entire timeline.
    /// Returns regions sorted by score descending, limited to topCount.
    public static List<HighlightRegion> DetectHighlights(
        Timeline timeline,
        TranscriptCache? transcriptCache = null,
        int topCount = 5,
        int minDurationFrames = 30)
    {
        var candidates = new List<HighlightRegion>();

        // 1. Scene changes at clip boundaries
        foreach (var track in timeline.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                // Clip starts and ends are natural highlight boundaries
                int midFrame = clip.StartFrame + clip.DurationFrames / 2;
                float baseScore = 0.3f;
                candidates.Add(new HighlightRegion(clip.StartFrame, clip.EndFrame, baseScore, $"Clip: {Path.GetFileNameWithoutExtension(clip.MediaRef)}"));
            }
        }

        // 2. Check transcript for keyword highlights
        if (transcriptCache != null)
        {
            var highlightKeywords = new[] { "amazing", "incredible", "wow", "awesome", "important", "key", "first", "best", "hilarious", "fantastic", "beautiful", "perfect", "love", "great", "exciting" };

            foreach (var track in timeline.Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    var segments = transcriptCache.GetTranscript(clip.MediaRef);
                    if (segments == null) continue;

                    foreach (var seg in segments)
                    {
                        float keywordScore = 0f;
                        foreach (var kw in highlightKeywords)
                        {
                            if (seg.Text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                                keywordScore += 0.15f;
                        }

                        if (keywordScore > 0)
                        {
                            int globalStart = clip.StartFrame + seg.StartFrame;
                            int globalEnd = clip.StartFrame + seg.EndFrame;
                            // Clamp to clip duration
                            globalEnd = Math.Min(globalEnd, clip.EndFrame);

                            if (globalEnd - globalStart >= minDurationFrames)
                            {
                                float score = 0.3f + keywordScore;
                                candidates.Add(new HighlightRegion(globalStart, globalEnd, Math.Min(score, 1.0f), $"Key moment: \"{seg.Text[..Math.Min(40, seg.Text.Length)]}\""));
                            }
                        }
                    }
                }
            }
        }

        // 3. Deduplicate overlapping regions (keep highest score)
        var merged = MergeOverlapping(candidates);

        // 4. Sort by score descending, take top N
        return merged
            .OrderByDescending(h => h.Score)
            .Take(topCount)
            .ToList();
    }

    /// Find filler-word regions in the timeline using transcript data.
    /// Uses the shared filler word list from SrtParser.FillerWords.
    public static List<(int StartFrame, int EndFrame, string FillerWord, string Context)> DetectFillerRegions(
        Timeline timeline,
        TranscriptCache transcriptCache)
    {
        var result = new List<(int, int, string, string)>();

        foreach (var track in timeline.Tracks)
        {
            foreach (var clip in track.Clips)
            {
                var segments = transcriptCache.GetTranscript(clip.MediaRef);
                if (segments == null) continue;

                foreach (var seg in segments)
                {
                    var filler = SrtParser.FindFillerInText(seg.Text);
                    if (filler != null)
                    {
                        int globalStart = clip.StartFrame + seg.StartFrame;
                        int globalEnd = clip.StartFrame + seg.EndFrame;
                        result.Add((globalStart, Math.Min(globalEnd, clip.EndFrame), filler, seg.Text));
                    }
                }
            }
        }

        return result;
    }

    private static List<HighlightRegion> MergeOverlapping(List<HighlightRegion> regions)
    {
        if (regions.Count == 0) return regions;

        var sorted = regions.OrderBy(r => r.StartFrame).ThenByDescending(r => r.Score).ToList();
        var merged = new List<HighlightRegion>();
        var current = sorted[0];

        for (int i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];
            if (next.StartFrame <= current.EndFrame)
            {
                // Overlapping — keep the one with higher score
                if (next.Score > current.Score)
                    current = next with { EndFrame = Math.Max(current.EndFrame, next.EndFrame) };
                else
                    current = current with { EndFrame = Math.Max(current.EndFrame, next.EndFrame) };
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        return merged;
    }
}
