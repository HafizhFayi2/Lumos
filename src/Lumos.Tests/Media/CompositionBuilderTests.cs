using System;
using System.Linq;
using Lumos.Domain;
using Lumos.Media;
using Xunit;

namespace Lumos.Tests.Media;

/// <summary>
/// Unit tests for CompositionBuilder — the component that resolves a
/// Timeline + playhead frame into CompositionSlots for the compositor.
/// </summary>
public class CompositionBuilderTests
{
    // ── Helpers ─────────────────────────────────────────────────

    private static Clip MakeClip(string id, string mediaRef, ClipType type,
        int start, int duration, int sourceDuration = 300, string? linkGroupId = null)
    {
        return new Clip
        {
            Id = id,
            MediaRef = mediaRef,
            MediaType = type,
            StartFrame = start,
            DurationFrames = duration,
            TrimStartFrame = 0,
            TrimEndFrame = sourceDuration - duration, // Derived
            LinkGroupId = linkGroupId,
        };
    }

    private static Timeline MakeTimeline(int fps = 30, int width = 1920, int height = 1080)
    {
        return new Timeline
        {
            Fps = fps,
            Width = width,
            Height = height,
            Tracks = new(),
        };
    }

    // ── Basic slot resolution ──────────────────────────────────

    [Fact]
    public void Build_NoTimeline_ReturnsEmpty()
    {
        var builder = new CompositionBuilder();
        var result = builder.Build(0);
        Assert.Same(CompositionFrame.Empty, result);
    }

    [Fact]
    public void Build_EmptyTimeline_ReturnsEmpty()
    {
        var timeline = MakeTimeline();
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(0);
        Assert.Empty(result.Visual);
        Assert.Empty(result.Audio);
    }

    [Fact]
    public void Build_SingleClip_ReturnsSlotForThatFrame()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "test.mp4", ClipType.Video, 10, 30));
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Frame within clip range
        var result = builder.Build(15);
        Assert.Single(result.Visual);
        Assert.Empty(result.Audio);

        var slot = result.Visual[0];
        Assert.Equal("c1", slot.Clip.Id);
        Assert.Equal("test.mp4", slot.AssetPath);
        Assert.Equal(ClipType.Video, slot.RenderType);
    }

    [Fact]
    public void Build_FrameOutsideClip_ReturnsNoSlot()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "test.mp4", ClipType.Video, 0, 30));
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Frame 31 is past clip end (0-29)
        var result = builder.Build(31);
        Assert.Empty(result.Visual);
    }

    [Fact]
    public void Build_FrameBeforeClip_ReturnsNoSlot()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "test.mp4", ClipType.Video, 10, 30));
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(5);
        Assert.Empty(result.Visual);
    }

    // ── Source frame calculation ────────────────────────────────

    [Fact]
    public void Build_SourceFrame_EqualsTrimStartPlusLocal()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "test.mp4", ClipType.Video, 10, 50);
        clip.TrimStartFrame = 100;
        clip.Speed = 1.0;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Timeline frame 20 → local frame = 20 - 10 = 10 → source frame = 100 + 10 = 110
        var result = builder.Build(20);
        Assert.Equal(110, result.Visual[0].SourceFrame);
    }

    [Fact]
    public void Build_SourceFrame_WithSpeedMultiplier()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "test.mp4", ClipType.Video, 0, 100);
        clip.TrimStartFrame = 0;
        clip.Speed = 2.0;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Timeline frame 10 → local frame = 10 → source frame = round(10 * 2.0) = 20
        var result = builder.Build(10);
        Assert.Equal(20, result.Visual[0].SourceFrame);
    }

    [Fact]
    public void Build_SourceFrame_WithHalfSpeed()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "test.mp4", ClipType.Video, 0, 100);
        clip.TrimStartFrame = 0;
        clip.Speed = 0.5;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Timeline frame 10 → local frame = 10 → source frame = round(10 * 0.5) = 5
        var result = builder.Build(10);
        Assert.Equal(5, result.Visual[0].SourceFrame);
    }

    [Fact]
    public void Build_SourceFrame_NeverNegative()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "test.mp4", ClipType.Video, 5, 30);
        clip.TrimStartFrame = 0;
        clip.Speed = 1.0;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Frame within clip but just barely — source should never be negative
        var result = builder.Build(5);
        Assert.True(result.Visual[0].SourceFrame >= 0);
    }

    // ── Audio / Visual slot separation ─────────────────────────

    [Fact]
    public void Build_AudioClip_GoesToAudioList()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio };
        track.Clips.Add(MakeClip("ac1", "audio.mp3", ClipType.Audio, 0, 100));
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(50);
        Assert.Empty(result.Visual);
        Assert.Single(result.Audio);
        Assert.Equal("ac1", result.Audio[0].Clip.Id);
    }

    [Fact]
    public void Build_VideoAndAudioTracks_SeparateSlots()
    {
        var timeline = MakeTimeline();
        var vTrack = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        vTrack.Clips.Add(MakeClip("vc1", "video.mp4", ClipType.Video, 0, 100));
        timeline.Tracks.Add(vTrack);

        var aTrack = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio };
        aTrack.Clips.Add(MakeClip("ac1", "voiceover.mp3", ClipType.Audio, 0, 100));
        timeline.Tracks.Add(aTrack);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(50);
        Assert.Single(result.Visual);
        Assert.Single(result.Audio);
    }

    // ── Track visibility and muting ────────────────────────────

    [Fact]
    public void Build_HiddenVideoTrack_ExcludesVisualSlots()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video, IsHidden = true };
        track.Clips.Add(MakeClip("c1", "hidden.mp4", ClipType.Video, 0, 30));
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(15);
        Assert.Empty(result.Visual);
    }

    [Fact]
    public void Build_MutedAudioTrack_ExcludesAudioSlots()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio, IsMuted = true };
        track.Clips.Add(MakeClip("ac1", "muted.mp3", ClipType.Audio, 0, 30));
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(15);
        Assert.Empty(result.Audio);
    }

    [Fact]
    public void Build_VisibleUnmutedTracks_IncludeBoth()
    {
        var timeline = MakeTimeline();
        var vTrack = new Track { Id = "V1", Name = "V1", Type = ClipType.Video, IsHidden = false };
        vTrack.Clips.Add(MakeClip("c1", "visible.mp4", ClipType.Video, 0, 30));
        timeline.Tracks.Add(vTrack);

        var aTrack = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio, IsMuted = false };
        aTrack.Clips.Add(MakeClip("ac1", "audible.mp3", ClipType.Audio, 0, 30));
        timeline.Tracks.Add(aTrack);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(15);
        Assert.Single(result.Visual);
        Assert.Single(result.Audio);
    }

    // ── Overlapping clips ──────────────────────────────────────

    [Fact]
    public void Build_OverlappingClips_ReturnsMultipleVisualSlots()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "first.mp4", ClipType.Video, 0, 50));
        track.Clips.Add(MakeClip("c2", "second.mp4", ClipType.Video, 20, 50)); // Overlaps c1
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        // Frame 30 is covered by both clips
        var result = builder.Build(30);
        Assert.Equal(2, result.Visual.Count);
    }

    [Fact]
    public void Build_NonOverlappingClips_SingleSlotPerFrame()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "first.mp4", ClipType.Video, 0, 20));
        track.Clips.Add(MakeClip("c2", "second.mp4", ClipType.Video, 20, 30)); // Adjacent
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result1 = builder.Build(19);
        Assert.Single(result1.Visual);
        Assert.Equal("c1", result1.Visual[0].Clip.Id);

        var result2 = builder.Build(20);
        Assert.Single(result2.Visual);
        Assert.Equal("c2", result2.Visual[0].Clip.Id);
    }

    // ── Opacity and volume timing ──────────────────────────────

    [Fact]
    public void Build_Opacity_AtDefaultIsOne()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "test.mp4", ClipType.Video, 0, 50);
        clip.Opacity = 0.75;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(10);
        Assert.Equal(0.75, result.Visual[0].Opacity);
    }

    [Fact]
    public void Build_Volume_AtDefaultIsOne()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio };
        var clip = MakeClip("ac1", "test.mp3", ClipType.Audio, 0, 50);
        clip.Volume = 0.5;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(10);
        Assert.Equal(0.5, result.Audio[0].Volume);
    }

    // ── Multiple tracks ────────────────────────────────────────

    [Fact]
    public void Build_MultipleVideoTracks_VisualSlotsReversed()
    {
        var timeline = MakeTimeline();
        var track1 = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track1.Clips.Add(MakeClip("c1", "bottom.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track1);

        var track2 = new Track { Id = "V2", Name = "V2", Type = ClipType.Video };
        track2.Clips.Add(MakeClip("c2", "top.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track2);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(10);

        // Visual slots should be in painter's order (reversed):
        // First in list = higher track index (top)
        // Track V2 (index 1) should come before V1 (index 0) in visual list
        Assert.Equal(2, result.Visual.Count);
        Assert.Equal("c2", result.Visual[0].Clip.Id); // Top track first in painter's order
        Assert.Equal("c1", result.Visual[1].Clip.Id); // Bottom track last
    }

    // ── Loop / load behavior ───────────────────────────────────

    [Fact]
    public void Build_ReloadNewTimeline_ResetsState()
    {
        var timeline1 = MakeTimeline();
        var track1 = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track1.Clips.Add(MakeClip("c1", "orig.mp4", ClipType.Video, 0, 30));
        timeline1.Tracks.Add(track1);

        var timeline2 = MakeTimeline();
        var track2 = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio };
        track2.Clips.Add(MakeClip("ac1", "replaced.mp3", ClipType.Audio, 0, 30));
        timeline2.Tracks.Add(track2);

        var builder = new CompositionBuilder();
        builder.Load(timeline1);
        builder.Load(timeline2);

        // After loading timeline2, we should only see its content
        var result = builder.Build(15);
        Assert.Empty(result.Visual);
        Assert.Single(result.Audio);
    }

    // ── Edge cases ─────────────────────────────────────────────

    [Fact]
    public void Build_ClipWithNoMediaRef_ReturnsSlotWithEmptyAssetPath()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(new Clip
        {
            Id = "empty",
            MediaRef = null,
            MediaType = ClipType.Video,
            StartFrame = 0,
            DurationFrames = 30,
        });
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(10);
        Assert.Single(result.Visual);
        Assert.Equal(string.Empty, result.Visual[0].AssetPath);
    }

    [Fact]
    public void Build_NullTrack_SkipsGracefully()
    {
        var timeline = MakeTimeline();
        timeline.Tracks.Add(null!); // Simulate a null track entry

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(0);
        Assert.Empty(result.Visual);
        Assert.Empty(result.Audio);
    }

    [Fact]
    public void Build_NullClipsOnTrack_SkipsGracefully()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips = null; // Simulate null clips collection
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(0);
        Assert.Empty(result.Visual);
        Assert.Empty(result.Audio);
    }

    [Fact]
    public void Build_EffectsOnClip_PropagatedToSlot()
    {
        var timeline = MakeTimeline();
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "fx_clip.mp4", ClipType.Video, 0, 30);
        clip.Effects = new()
        {
            new() { Type = "glow", Enabled = true },
            new() { Type = "vignette", Enabled = true },
        };
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var result = builder.Build(15);
        Assert.Single(result.Visual);
        Assert.Equal(2, result.Visual[0].Effects.Count);
        Assert.Equal("glow", result.Visual[0].Effects[0].Type);
        Assert.Equal("vignette", result.Visual[0].Effects[1].Type);
    }
}
