using Lumos.Application.Commands;
using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Commands;

public sealed class TrimClipCommandTests
{
    [Fact]
    public async Task TrimStart_ShiftsClipStartAndShortensDuration()
    {
        // A 60-frame clip starting at 0; trim 10 frames from the head
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new TrimClipAsyncCommand(clip.Id, newTrimStartFrame: 10, newTrimEndFrame: 0);

        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        var updated = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        Assert.Equal(10, updated.TrimStartFrame);
        Assert.Equal(10, updated.StartFrame);   // moved right by 10
        Assert.Equal(50, updated.DurationFrames); // shorter by 10
    }

    [Fact]
    public async Task TrimEnd_ShortensDurationWithoutMovingStart()
    {
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new TrimClipAsyncCommand(clip.Id, newTrimStartFrame: 0, newTrimEndFrame: 10);

        await CommandTestHelper.RunAsync(store, cmd);

        var updated = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        Assert.Equal(10, updated.TrimEndFrame);
        Assert.Equal(0,  updated.StartFrame);   // start unchanged
        Assert.Equal(50, updated.DurationFrames);
    }

    [Fact]
    public async Task Trim_UndoRestoresOriginal()
    {
        var tl    = CommandTestHelper.OneTrack((0, 60));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new TrimClipAsyncCommand(clip.Id, newTrimStartFrame: 5, newTrimEndFrame: 0);

        await CommandTestHelper.RunAsync(store, cmd);
        await CommandTestHelper.UndoAsync(store, cmd);

        var restored = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        Assert.Equal(0,  restored.TrimStartFrame);
        Assert.Equal(0,  restored.StartFrame);
        Assert.Equal(60, restored.DurationFrames);
    }
}
