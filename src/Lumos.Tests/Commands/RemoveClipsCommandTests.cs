using Lumos.Application.Commands;
using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Commands;

public sealed class RemoveClipsCommandTests
{
    [Fact]
    public async Task Remove_EliminatesClipFromTrack()
    {
        var tl    = CommandTestHelper.OneTrack((0, 30), (40, 30));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new RemoveClipsAsyncCommand(new[] { clip.Id });

        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        var clips = store.State.Timeline.Timeline.Tracks[0].Clips;
        Assert.Single(clips);
        Assert.DoesNotContain(clips, c => c.Id == clip.Id);
    }

    [Fact]
    public async Task Remove_UndoRestoresBothClips()
    {
        var tl    = CommandTestHelper.OneTrack((0, 30), (40, 30));
        var store = CommandTestHelper.MakeStore(tl);
        var clip  = store.State.Timeline.Timeline.Tracks[0].Clips[0];
        var cmd   = new RemoveClipsAsyncCommand(new[] { clip.Id });

        await CommandTestHelper.RunAsync(store, cmd);
        await CommandTestHelper.UndoAsync(store, cmd);

        Assert.Equal(2, store.State.Timeline.Timeline.Tracks[0].Clips.Count);
    }

    [Fact]
    public async Task Remove_UnknownIdIsNoOp()
    {
        var tl    = CommandTestHelper.OneTrack((0, 30));
        var store = CommandTestHelper.MakeStore(tl);
        var cmd   = new RemoveClipsAsyncCommand(new[] { Guid.NewGuid().ToString() });

        await CommandTestHelper.RunAsync(store, cmd);

        Assert.Single(store.State.Timeline.Timeline.Tracks[0].Clips);
    }
}
