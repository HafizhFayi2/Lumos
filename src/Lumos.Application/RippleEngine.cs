using Palmier.Domain;

namespace Palmier.Application;

/// A proposed new start frame for a single clip, produced by the ripple engine
/// and applied by the caller.
public record struct ClipShift(string ClipId, int NewStartFrame);

/// A half-open [start, end) frame interval on a single track. Used to describe
/// the gaps that a ripple edit needs to close.
public record struct FrameRange(int Start, int End)
{
    public int Length => End - Start;
}

/// A user-selected empty gap on a single track
public record struct GapSelection(int TrackIndex, FrameRange Range);

/// Pure functions for ripple editing: computing how clips shift after
/// insertions or deletions.
public static class RippleEngine
{
    /// After removing clips from a track, compute new start frames for
    /// remaining clips that should shift backward to close the gap.
    public static List<ClipShift> ComputeRippleShifts(IEnumerable<Clip> clips, HashSet<string> removedIds)
    {
        var removedRanges = clips
            .Where(c => removedIds.Contains(c.Id))
            .Select(c => new FrameRange(c.StartFrame, c.EndFrame))
            .ToList();

        var remainingClips = clips.Where(c => !removedIds.Contains(c.Id));

        return ComputeRippleShiftsForRanges(remainingClips, removedRanges);
    }

    /// Shift clips leftward to close the gaps defined by `removedRanges`.
    /// Used when ranges come from a different track (sync-locked ripple).
    public static List<ClipShift> ComputeRippleShiftsForRanges(IEnumerable<Clip> clips, IEnumerable<FrameRange> removedRanges)
    {
        var merged = MergeRanges(removedRanges);
        if (merged.Count == 0) return new List<ClipShift>();

        var shifts = new List<ClipShift>();
        var sortedClips = clips.OrderBy(c => c.StartFrame);

        foreach (var clip in sortedClips)
        {
            int shift = merged
                .Where(r => r.End <= clip.StartFrame)
                .Sum(r => r.Length);

            if (shift > 0)
            {
                shifts.Add(new ClipShift(clip.Id, clip.StartFrame - shift));
            }
        }
        return shifts;
    }

    /// Push all clips at or after `insertFrame` forward by `pushAmount` frames.
    public static List<ClipShift> ComputeRipplePush(
        IEnumerable<Clip> clips,
        int insertFrame,
        int pushAmount,
        HashSet<string>? excludeIds = null)
    {
        excludeIds ??= new HashSet<string>();
        return clips
            .Where(c => !excludeIds.Contains(c.Id) && c.StartFrame >= insertFrame)
            .Select(c => new ClipShift(c.Id, c.StartFrame + pushAmount))
            .ToList();
    }

    // Helper: merge overlapping and adjacent ranges
    public static List<FrameRange> MergeRanges(IEnumerable<FrameRange> ranges)
    {
        var sorted = ranges.OrderBy(r => r.Start).ToList();
        var merged = new List<FrameRange>();
        foreach (var range in sorted)
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new FrameRange(last.Start, Math.Max(last.End, range.End));
            }
            else
            {
                merged.Add(range);
            }
        }
        return merged;
    }
}
