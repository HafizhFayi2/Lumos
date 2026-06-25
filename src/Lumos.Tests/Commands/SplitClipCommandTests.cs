using Lumos.Application.Commands;
using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Commands;

public sealed class SplitClipCommandTests
{
    [Fact]
    public async Task Split_ProducesTwoClipsAtCorrectBoundary()
    {
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new SplitClipAsyncCommand(clip.Id, 30);

        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        var clips = store.State.Timeline.Timeline.Tracks[0].Clips;
        Assert.Equal(2, clips.Count);
        Assert.Equal(0,  clips[0].StartFrame);
        Assert.Equal(30, clips[0].DurationFrames);
        Assert.Equal(30, clips[1].StartFrame);
        Assert.Equal(30, clips[1].DurationFrames);
    }

    [Fact]
    public async Task Split_UndoRestoresOriginalSingleClip()
    {
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new SplitClipAsyncCommand(clip.Id, 20);

        await CommandTestHelper.RunAsync(store, cmd);
        await CommandTestHelper.UndoAsync(store, cmd);

        var clips = store.State.Timeline.Timeline.Tracks[0].Clips;
        Assert.Single(clips);
        Assert.Equal(0,  clips[0].StartFrame);
        Assert.Equal(60, clips[0].DurationFrames);
    }

    [Fact]
    public async Task Split_AtStartFrameIsNoOp()
    {
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new SplitClipAsyncCommand(clip.Id, 0); // split at start → invalid

        await CommandTestHelper.RunAsync(store, cmd);

        Assert.Single(store.State.Timeline.Timeline.Tracks[0].Clips);
    }

    [Fact]
    public async Task Split_AtEndFrameIsNoOp()
    {
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new SplitClipAsyncCommand(clip.Id, 60); // split at end → invalid

        await CommandTestHelper.RunAsync(store, cmd);

        Assert.Single(store.State.Timeline.Timeline.Tracks[0].Clips);
    }
}
