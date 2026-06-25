using Lumos.Application.Commands;
using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Commands;

public sealed class MoveClipCommandTests
{
    private static Timeline TwoTracks(params (int start, int duration)[] v1Clips)
    {
        var t1 = new Track { Name = "V1", Type = ClipType.Video };
        foreach (var (start, dur) in v1Clips)
        {
            t1.Clips.Add(new Clip
            {
                MediaRef = "test.mp4",
                MediaType = ClipType.Video,
                StartFrame = start,
                DurationFrames = dur
            });
        }

        var t2 = new Track { Name = "V2", Type = ClipType.Video };

        return new Timeline
        {
            Fps = 30,
            Width = 1920,
            Height = 1080,
            Tracks = new() { t1, t2 }
        };
    }

    [Fact]
    public async Task Move_ToNewTrack_MovesClipSuccessfully()
    {
        var tl = TwoTracks((0, 30));
        var store = CommandTestHelper.MakeStore(tl);
        
        var t1 = store.State.Timeline.Timeline.Tracks[0];
        var t2 = store.State.Timeline.Timeline.Tracks[1];
        var clip = t1.Clips[0];
        
        var cmd = new MoveClipAsyncCommand(clip.Id, 15, t2.Id);
        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        Assert.Empty(t1.Clips);
        Assert.Single(t2.Clips);
        
        var moved = t2.Clips[0];
        Assert.Equal(clip.Id, moved.Id);
        Assert.Equal(15, moved.StartFrame);
        Assert.Equal(30, moved.DurationFrames);
    }

    [Fact]
    public async Task Move_WithinSameTrack_ShiftsStartFrame()
    {
        var tl = TwoTracks((0, 30));
        var store = CommandTestHelper.MakeStore(tl);
        
        var t1 = store.State.Timeline.Timeline.Tracks[0];
        var clip = t1.Clips[0];
        
        var cmd = new MoveClipAsyncCommand(clip.Id, 50, t1.Id);
        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        Assert.Single(t1.Clips);
        Assert.Equal(50, t1.Clips[0].StartFrame);
    }

    [Fact]
    public async Task Move_UndoRestoresOriginalState()
    {
        var tl = TwoTracks((0, 30));
        var store = CommandTestHelper.MakeStore(tl);
        
        var t1 = store.State.Timeline.Timeline.Tracks[0];
        var t2 = store.State.Timeline.Timeline.Tracks[1];
        var clip = t1.Clips[0];
        
        var cmd = new MoveClipAsyncCommand(clip.Id, 20, t2.Id);
        await CommandTestHelper.RunAsync(store, cmd);
        await CommandTestHelper.UndoAsync(store, cmd);

        var afterT1 = store.State.Timeline.Timeline.Tracks[0];
        var afterT2 = store.State.Timeline.Timeline.Tracks[1];

        Assert.Single(afterT1.Clips);
        Assert.Empty(afterT2.Clips);
        Assert.Equal(0, afterT1.Clips[0].StartFrame);
    }

    [Fact]
    public async Task Move_WithOverwrite_TrimsOrRemovesOverlappingClips()
    {
        // V1 has a clip at (0, 30).
        // V2 has a clip at (20, 20).
        var tl = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        var t1 = new Track { Name = "V1", Type = ClipType.Video };
        var clip1 = new Clip { MediaRef = "c1.mp4", MediaType = ClipType.Video, StartFrame = 0, DurationFrames = 30 };
        t1.Clips.Add(clip1);

        var t2 = new Track { Name = "V2", Type = ClipType.Video };
        var clip2 = new Clip { MediaRef = "c2.mp4", MediaType = ClipType.Video, StartFrame = 20, DurationFrames = 20 };
        t2.Clips.Add(clip2);

        tl.Tracks.Add(t1);
        tl.Tracks.Add(t2);

        var store = CommandTestHelper.MakeStore(tl);
        t1 = store.State.Timeline.Timeline.Tracks[0];
        t2 = store.State.Timeline.Timeline.Tracks[1];
        clip1 = t1.Clips[0];
        clip2 = t2.Clips[0];

        // Move clip1 to V2 starting at frame 10 (duration 30: frames 10 to 40)
        // clip2 (20 to 40) should be overwritten/removed or split/trimmed.
        // OverwriteEngine will determine action based on range 10-40, which completely covers 20-40.
        // Let's test this move.
        var cmd = new MoveClipAsyncCommand(clip1.Id, 10, t2.Id);
        var (ok, _) = await CommandTestHelper.RunAsync(store, cmd);

        Assert.True(ok);
        Assert.Empty(t1.Clips);
        
        // clip2 should be overwritten (removed/trimmed) because 10-40 spans all or part of it.
        // Since clip2 is at 20-40, it is completely covered by 10-40, so it should be removed.
        Assert.Single(t2.Clips);
        Assert.Equal(clip1.Id, t2.Clips[0].Id);
        Assert.Equal(10, t2.Clips[0].StartFrame);
    }
}
