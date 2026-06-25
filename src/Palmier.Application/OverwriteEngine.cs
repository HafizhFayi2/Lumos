using Palmier.Domain;

namespace Palmier.Application;

public abstract record OverwriteAction
{
    public record Remove(string ClipId) : OverwriteAction;
    public record TrimEnd(string ClipId, int NewDuration) : OverwriteAction;
    public record TrimStart(string ClipId, int NewStartFrame, int NewTrimStart, int NewDuration) : OverwriteAction;
    public record Split(
        string ClipId,
        int LeftDuration,
        string RightId,
        int RightStartFrame,
        int RightTrimStart,
        int RightDuration
    ) : OverwriteAction;
}

/// Pure functions for overwrite editing: computing how to clear a region
/// of the timeline by removing, trimming, or splitting existing clips.
public static class OverwriteEngine
{
    /// Given a region [regionStart, regionEnd) on a track, returns the actions
    /// needed to clear that region so a new clip can be placed there.
    public static List<OverwriteAction> ComputeOverwrite(
        IEnumerable<Clip> clips,
        int regionStart,
        int regionEnd)
    {
        if (regionEnd <= regionStart) return new List<OverwriteAction>();
        var actions = new List<OverwriteAction>();

        foreach (var clip in clips)
        {
            int cs = clip.StartFrame;
            int ce = clip.EndFrame;

            if (ce <= regionStart || cs >= regionEnd)
            {
                continue;
            }

            if (cs >= regionStart && ce <= regionEnd)
            {
                actions.Add(new OverwriteAction.Remove(clip.Id));
            }
            else if (cs < regionStart && ce > regionEnd)
            {
                int leftDuration = regionStart - cs;
                int rightStartFrame = regionEnd;
                int rightTrimStart = clip.TrimStartFrame + (int)Math.Round((regionEnd - cs) * clip.Speed);
                int rightDuration = ce - regionEnd;
                actions.Add(new OverwriteAction.Split(
                    clip.Id,
                    leftDuration,
                    Guid.NewGuid().ToString(),
                    rightStartFrame,
                    rightTrimStart,
                    rightDuration
                ));
            }
            else if (cs < regionStart)
            {
                // Overlaps left side — trim right edge
                int newDuration = regionStart - cs;
                actions.Add(new OverwriteAction.TrimEnd(clip.Id, newDuration));
            }
            else
            {
                // Overlaps right side — trim left edge
                int trimAmount = regionEnd - cs;
                int newStartFrame = regionEnd;
                int newTrimStart = clip.TrimStartFrame + (int)Math.Round(trimAmount * clip.Speed);
                int newDuration = ce - regionEnd;
                actions.Add(new OverwriteAction.TrimStart(
                    clip.Id,
                    newStartFrame,
                    newTrimStart,
                    newDuration
                ));
            }
        }

        return actions;
    }
}
