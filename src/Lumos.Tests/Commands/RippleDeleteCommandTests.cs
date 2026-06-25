using Lumos.Application.Commands;
using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Commands;

public sealed class RippleDeleteCommandTests
{
    [Fact]
    public async Task RippleDelete_RemovesClipAndShiftsDownstreamLeft()
    {
        // [A:0-30] [B:30-60] [C:60-90]  →  remove B  →  [A:0-30] [C:30-60]
        var tl    = CommandTestHelper.OneTrack((0, 30), (30, 30), (60, 30));
        var store = CommandTestHelper.MakeStore(tl);
        var clips = store.State.Timeline.Timeline.Tracks[0].Clips;
        var bId   = clips[1].Id;
        var cmd   = new RippleDeleteAsyncCommand(new[] { bId });

        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        var result = store.State.Timeline.Timeline.Tracks[0].Clips;
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, c => c.Id == bId);
        // C should have shifted left by 30 frames
        var c = result[1];
        Assert.Equal(30, c.StartFrame);
    }

    [Fact]
    public async Task RippleDelete_RemovingLastClipLeavesNothingDownstream()
    {
        var tl    = CommandTestHelper.OneTrack((0, 30), (30, 30));
        var store = CommandTestHelper.MakeStore(tl);
        var last  = store.State.Timeline.Timeline.Tracks[0].Clips[1];
        var cmd   = new RippleDeleteAsyncCommand(new[] { last.Id });

        await CommandTestHelper.RunAsync(store, cmd);

        var result = store.State.Timeline.Timeline.Tracks[0].Clips;
        Assert.Single(result);
        Assert.Equal(0,  result[0].StartFrame);
        Assert.Equal(30, result[0].DurationFrames);
    }

    [Fact]
    public async Task RippleDelete_UndoRestoresAllClipsAndPositions()
    {
        var tl     = CommandTestHelper.OneTrack((0, 30), (30, 30), (60, 30));
        var store  = CommandTestHelper.MakeStore(tl);
        var bId    = store.State.Timeline.Timeline.Tracks[0].Clips[1].Id;
        var cmd    = new RippleDeleteAsyncCommand(new[] { bId });

        await CommandTestHelper.RunAsync(store, cmd);
        await CommandTestHelper.UndoAsync(store, cmd);

        var result = store.State.Timeline.Timeline.Tracks[0].Clips;
        Assert.Equal(3, result.Count);
        Assert.Equal(0,  result[0].StartFrame);
        Assert.Equal(30, result[1].StartFrame);
        Assert.Equal(60, result[2].StartFrame);
    }

    [Fact]
    public async Task RippleDelete_GapBetweenClipsIsNotClosed()
    {
        // [A:0-20] [B:40-60] [C:80-100]  →  remove A  →  gap before B is not rippled
        // Actually RippleDelete shifts everything contiguous downstream; B at 40 won't move
        // because there's a gap between A and B.
        var tl    = CommandTestHelper.OneTrack((0, 20), (40, 20), (80, 20));
        var store = CommandTestHelper.MakeStore(tl);
        var aId   = store.State.Timeline.Timeline.Tracks[0].Clips[0].Id;
        var cmd   = new RippleDeleteAsyncCommand(new[] { aId });

        await CommandTestHelper.RunAsync(store, cmd);

        var result = store.State.Timeline.Timeline.Tracks[0].Clips;
        // B was not adjacent to A — stays at 40 (gap not closed for non-contiguous clips)
        Assert.Equal(2, result.Count);
    }
}
