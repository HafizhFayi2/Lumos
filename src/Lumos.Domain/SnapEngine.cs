using System;
using System.Collections.Generic;
using System.Linq;

namespace Palmier.Domain;

public enum SnapTargetKind
{
    Playhead,
    ClipEdge
}

public readonly record struct SnapTarget(int Frame, SnapTargetKind Kind);

public readonly record struct SnapResult(int Frame, int ProbeOffset, double X);

public struct SnapState
{
    public int? CurrentlySnappedTo { get; set; }
    public int CurrentProbeOffset { get; set; }

    public SnapState()
    {
        CurrentlySnappedTo = null;
        CurrentProbeOffset = 0;
    }
}

public static class SnapEngine
{
    public static List<SnapTarget> CollectTargets(
        IEnumerable<Track> tracks,
        int playheadFrame = 0,
        HashSet<string>? excludeClipIds = null,
        bool includePlayhead = false)
    {
        excludeClipIds ??= new HashSet<string>();
        var targets = new List<SnapTarget>();

        if (includePlayhead)
        {
            targets.Add(new SnapTarget(playheadFrame, SnapTargetKind.Playhead));
        }

        foreach (var track in tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (!excludeClipIds.Contains(clip.Id))
                {
                    targets.Add(new SnapTarget(clip.StartFrame, SnapTargetKind.ClipEdge));
                    targets.Add(new SnapTarget(clip.EndFrame, SnapTargetKind.ClipEdge));
                }
            }
        }

        return targets;
    }

    public static SnapResult? FindSnap(
        int position,
        List<int> probeOffsets,
        List<SnapTarget> targets,
        ref SnapState state,
        double baseThreshold,
        double pixelsPerFrame)
    {
        double baseFrameThreshold = baseThreshold / pixelsPerFrame;

        if (state.CurrentlySnappedTo.HasValue)
        {
            int snapped = state.CurrentlySnappedTo.Value;
            double holdThreshold = baseFrameThreshold * Snap.StickyMultiplier;
            int probePos = position + state.CurrentProbeOffset;
            if (Math.Abs(probePos - snapped) <= holdThreshold && targets.Any(t => t.Frame == snapped))
            {
                return new SnapResult(snapped, state.CurrentProbeOffset, snapped * pixelsPerFrame);
            }
            state.CurrentlySnappedTo = null;
            state.CurrentProbeOffset = 0;
        }

        int bestProbeOffset = 0;
        SnapTarget? bestTarget = null;
        double bestDistance = double.PositiveInfinity;

        foreach (int probeOffset in probeOffsets)
        {
            int probePos = position + probeOffset;
            foreach (var target in targets)
            {
                double threshold = target.Kind == SnapTargetKind.Playhead
                    ? baseFrameThreshold * Snap.PlayheadMultiplier
                    : baseFrameThreshold;

                double dist = Math.Abs(probePos - target.Frame);
                if (dist <= threshold && dist < bestDistance)
                {
                    bestProbeOffset = probeOffset;
                    bestTarget = target;
                    bestDistance = dist;
                }
            }
        }

        if (bestTarget == null) return null;

        state.CurrentlySnappedTo = bestTarget.Value.Frame;
        state.CurrentProbeOffset = bestProbeOffset;

        return new SnapResult(bestTarget.Value.Frame, bestProbeOffset, bestTarget.Value.Frame * pixelsPerFrame);
    }
}
