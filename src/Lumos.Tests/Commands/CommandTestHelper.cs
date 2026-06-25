using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.Tests.Commands;

/// Minimal test harness: runs a TimelineCommand directly against an EditorStore
/// without starting a CommandQueue background thread.
internal static class CommandTestHelper
{
    internal static EditorStore MakeStore(Timeline timeline)
    {
        var state = new EditorState { Timeline = TimelineState.Initial() };
        var store = new EditorStore(state);
        // Seed timeline directly via a silent mutation
        store.MutateTimeline("seed", tl =>
        {
            tl.Fps    = timeline.Fps;
            tl.Width  = timeline.Width;
            tl.Height = timeline.Height;
            tl.Tracks = timeline.Tracks.Select(t => t.Clone()).ToList();
            tl.SettingsConfigured = timeline.SettingsConfigured;
        });
        return store;
    }

    internal static async Task<(bool ok, string? error)> RunAsync(
        EditorStore store,
        TimelineCommand cmd)
    {
        var ctx    = new CommandContext(store);
        var result = await cmd.ExecuteAsync(ctx);
        return (result.Succeeded, result.ErrorMessage);
    }

    internal static async Task<(bool ok, string? error)> UndoAsync(
        EditorStore store,
        TimelineCommand cmd)
    {
        var ctx    = new CommandContext(store);
        var result = await cmd.UndoAsync(ctx);
        return (result.Succeeded, result.ErrorMessage);
    }

    /// Build a Timeline with a single track and the supplied clips.
    internal static Timeline OneTrack(params (int start, int duration)[] clips)
    {
        var track = new Track { Name = "V1", Type = ClipType.Video };
        foreach (var (start, dur) in clips)
            track.Clips.Add(new Clip
            {
                MediaRef       = "test.mp4",
                MediaType      = ClipType.Video,
                StartFrame     = start,
                DurationFrames = dur,
            });
        return new Timeline { Fps = 30, Width = 1920, Height = 1080, Tracks = new() { track } };
    }
}
