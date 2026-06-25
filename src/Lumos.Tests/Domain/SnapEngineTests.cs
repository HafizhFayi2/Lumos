using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Domain;

public sealed class SnapEngineTests
{
    private static Track MakeTrack(params (int start, int duration)[] clips)
    {
        var track = new Track { Name = "Test" };
        foreach (var (start, dur) in clips)
            track.Clips.Add(new Clip { StartFrame = start, DurationFrames = dur });
        return track;
    }

    [Fact]
    public void CollectTargets_ReturnsStartAndEndEdgesForEachClip()
    {
        var track = MakeTrack((0, 10), (20, 30));
        var targets = SnapEngine.CollectTargets(new[] { track });

        Assert.Contains(targets, t => t.Frame == 0  && t.Kind == SnapTargetKind.ClipEdge);
        Assert.Contains(targets, t => t.Frame == 10 && t.Kind == SnapTargetKind.ClipEdge);
        Assert.Contains(targets, t => t.Frame == 20 && t.Kind == SnapTargetKind.ClipEdge);
        Assert.Contains(targets, t => t.Frame == 50 && t.Kind == SnapTargetKind.ClipEdge);
    }

    [Fact]
    public void CollectTargets_IncludesPlayheadWhenRequested()
    {
        var track = MakeTrack((0, 10));
        var targets = SnapEngine.CollectTargets(new[] { track }, playheadFrame: 15, includePlayhead: true);
        Assert.Contains(targets, t => t.Frame == 15 && t.Kind == SnapTargetKind.Playhead);
    }

    [Fact]
    public void CollectTargets_ExcludesSpecifiedClipIds()
    {
        var track = MakeTrack((0, 10), (20, 5));
        var excludeId = track.Clips[0].Id;
        var targets = SnapEngine.CollectTargets(new[] { track }, excludeClipIds: new HashSet<string> { excludeId });

        Assert.DoesNotContain(targets, t => t.Frame == 0);
        Assert.DoesNotContain(targets, t => t.Frame == 10);
        Assert.Contains(targets, t => t.Frame == 20);
    }

    [Fact]
    public void FindSnap_SnapsWhenWithinThreshold()
    {
        var targets = new List<SnapTarget> { new(100, SnapTargetKind.ClipEdge) };
        var probes = new List<int> { 0 };
        var state = new SnapState();
        double ppf = 4.0;
        double threshold = Snap.ThresholdPixels; // 8px → 2 frames at 4ppf

        var result = SnapEngine.FindSnap(101, probes, targets, ref state, threshold, ppf);

        Assert.NotNull(result);
        Assert.Equal(100, result!.Value.Frame);
    }

    [Fact]
    public void FindSnap_ReturnsNullWhenOutsideThreshold()
    {
        var targets = new List<SnapTarget> { new(100, SnapTargetKind.ClipEdge) };
        var probes = new List<int> { 0 };
        var state = new SnapState();
        double ppf = 4.0;

        // 20 frames away, threshold = 8px/4ppf = 2 frames
        var result = SnapEngine.FindSnap(120, probes, targets, ref state, Snap.ThresholdPixels, ppf);

        Assert.Null(result);
    }

    [Fact]
    public void FindSnap_SticksWhileInsideStickyRadius()
    {
        var targets = new List<SnapTarget> { new(100, SnapTargetKind.ClipEdge) };
        var probes = new List<int> { 0 };
        double ppf = 4.0;
        var state = new SnapState();

        // First call — snap
        SnapEngine.FindSnap(101, probes, targets, ref state, Snap.ThresholdPixels, ppf);
        Assert.Equal(100, state.CurrentlySnappedTo);

        // Second call — still within sticky radius (1.5× threshold)
        var result = SnapEngine.FindSnap(102, probes, targets, ref state, Snap.ThresholdPixels, ppf);
        Assert.NotNull(result);
        Assert.Equal(100, result!.Value.Frame);
    }
}
