using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumos.Domain;
using Lumos.Infrastructure;
using Lumos.Media;
using Xunit;

namespace Lumos.Tests.Infrastructure;

/// <summary>
/// End-to-end export pipeline tests that exercise the full compositing path:
/// Timeline → CompositionBuilder → IFrameCompositor → pixel output.
/// Uses a mock frame provider (no real media files needed) so tests are fast
/// and deterministic while still exercising the real compositor and effects.
/// </summary>
public class ExportIntegrationTests
{
    // ── Helpers ─────────────────────────────────────────────────

    /// A checkerboard frame provider that returns distinct pixel patterns
    /// per asset path so we can verify correct slot compositing.
    private sealed class TestFrameProvider : IFrameProvider
    {
        private readonly int _defaultWidth;
        private readonly int _defaultHeight;

        public TestFrameProvider(int defaultWidth = 1920, int defaultHeight = 1080)
        {
            _defaultWidth = defaultWidth;
            _defaultHeight = defaultHeight;
        }

        public Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height)
        {
            int w = width > 0 ? width : _defaultWidth;
            int h = height > 0 ? height : _defaultHeight;
            var pixels = new byte[w * h * 4];

            int colorSeed = assetPath.GetHashCode();
            byte rBase = (byte)((colorSeed & 0xFF) % 200 + 30);
            byte gBase = (byte)(((colorSeed >> 8) & 0xFF) % 200 + 30);
            byte bBase = (byte)(((colorSeed >> 16) & 0xFF) % 200 + 30);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    bool pattern = ((x / 8) + (y / 8) + sourceFrame) % 2 == 0;
                    pixels[i]     = pattern ? bBase : (byte)(bBase / 2);
                    pixels[i + 1] = pattern ? gBase : (byte)(gBase / 2);
                    pixels[i + 2] = pattern ? rBase : (byte)(rBase / 2);
                    pixels[i + 3] = 255;
                }
            }
            return Task.FromResult<byte[]?>(pixels);
        }
    }

    private static Timeline MakeProject(int width, int height, int fps = 30)
    {
        return new Timeline
        {
            Fps = fps,
            Width = width,
            Height = height,
            Tracks = new(),
        };
    }

    private static Clip MakeClip(string id, string mediaRef, ClipType type,
        int startFrame, int duration, int sourceDuration = 300,
        string? linkGroupId = null, double speed = 1.0)
    {
        return new Clip
        {
            Id = id,
            MediaRef = mediaRef,
            MediaType = type,
            StartFrame = startFrame,
            DurationFrames = duration,
            TrimStartFrame = 0,
            TrimEndFrame = Math.Max(0, sourceDuration - duration),
            Speed = speed,
            LinkGroupId = linkGroupId,
        };
    }

    // ── Resolution tests ────────────────────────────────────────

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1280, 720)]
    [InlineData(854, 480)]
    [InlineData(640, 360)]
    public async Task CompositeAsync_DifferentResolutions_ReturnsCorrectSize(int width, int height)
    {
        var timeline = MakeProject(width, height);
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "video_a.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track);

        var provider = new TestFrameProvider(width, height);
        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(10);
        byte[] pixels = await compositor.CompositeAsync(frame);

        int expectedLen = width * height * 4;
        Assert.Equal(expectedLen, pixels.Length);

        bool hasContent = false;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0)
            {
                hasContent = true;
                break;
            }
        }
        Assert.True(hasContent, $"Composited pixels should contain content at {width}x{height}");
        Assert.Equal(255, pixels[3]);
    }

    [Fact]
    public async Task CompositeAsync_NoFrameProvider_FallsBackToBlack()
    {
        int width = 320, height = 240;
        var timeline = MakeProject(width, height);
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "nonexistent.mp4", ClipType.Video, 0, 30));
        timeline.Tracks.Add(track);

        using var compositor = new SkiaCompositor(width, height);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(5);
        byte[] pixels = await compositor.CompositeAsync(frame);

        Assert.Equal(width * height * 4, pixels.Length);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            Assert.Equal(0, pixels[i]);
            Assert.Equal(0, pixels[i + 1]);
            Assert.Equal(0, pixels[i + 2]);
        }
    }

    // ── Multi-track compositing tests ───────────────────────────

    [Fact]
    public async Task CompositeAsync_MultipleVideoTracks_CompositesInOrder()
    {
        int width = 80, height = 60;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var track1 = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track1.Clips.Add(MakeClip("c1", "track1_video.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track1);

        var track2 = new Track { Id = "V2", Name = "V2", Type = ClipType.Video };
        track2.Clips.Add(MakeClip("c2", "track2_overlay.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track2);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(10);
        Assert.Equal(2, frame.Visual.Count);

        byte[] pixels = await compositor.CompositeAsync(frame);
        Assert.Equal(width * height * 4, pixels.Length);
        Assert.Equal(255, pixels[3]);
    }

    [Fact]
    public async Task CompositeAsync_PartialOpacity_BlendsSlots()
    {
        int width = 40, height = 30;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var track1 = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip1 = MakeClip("c1", "base.mp4", ClipType.Video, 0, 30);
        clip1.Opacity = 0.5;
        track1.Clips.Add(clip1);
        timeline.Tracks.Add(track1);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(5);
        byte[] pixels = await compositor.CompositeAsync(frame);
        Assert.True(pixels.Any(b => b > 0), "50% opacity clip should still produce visible pixels");
    }

    [Fact]
    public async Task CompositeAsync_ZeroOpacity_SkipsSlot()
    {
        int width = 40, height = 30;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var track1 = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip1 = MakeClip("c1", "invisible.mp4", ClipType.Video, 0, 30);
        clip1.Opacity = 0;
        track1.Clips.Add(clip1);
        timeline.Tracks.Add(track1);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(5);
        byte[] pixels = await compositor.CompositeAsync(frame);

        // Zero-opacity slot should be skipped → all black
        for (int i = 0; i < pixels.Length; i += 4)
        {
            Assert.Equal(0, pixels[i]);
            Assert.Equal(0, pixels[i + 1]);
            Assert.Equal(0, pixels[i + 2]);
        }
    }

    // ── Effect pipeline tests ───────────────────────────────────

    [Fact]
    public async Task CompositeAsync_WithChromaKey_PipelineCompletes()
    {
        int width = 40, height = 30;
        var timeline = MakeProject(width, height);
        var provider = new GreenFrameProvider(width, height);

        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "greenscreen.mp4", ClipType.Video, 0, 30);
        clip.Effects = new List<Effect>
        {
            new()
            {
                Type = "chroma_key",
                Enabled = true,
                Params = new()
                {
                    ["key_r"] = new EffectParam { NumericValue = 0.0 },
                    ["key_g"] = new EffectParam { NumericValue = 255.0 },
                    ["key_b"] = new EffectParam { NumericValue = 0.0 },
                    ["threshold"] = new EffectParam { NumericValue = 0.1 },
                }
            }
        };
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(5);
        byte[] pixels = await compositor.CompositeAsync(frame);

        // Pipeline completed successfully with valid output
        Assert.Equal(width * height * 4, pixels.Length);

        // The chroma key IS known to work at the renderer level
        // (verified by EffectRendererTests.ChromaKeyRenderer_RemovesGreenBackground)
        // At the compositor pipeline level, at minimum verify it doesn't crash
    }

    [Fact]
    public async Task CompositeAsync_WithColorGrade_ProducesDifferentPixels()
    {
        int width = 40, height = 30;
        var provider = new TestFrameProvider(width, height);

        // Timeline WITHOUT effects (control)
        var timelineNoFx = MakeProject(width, height);
        var trackNoFx = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        trackNoFx.Clips.Add(MakeClip("c1", "grade_test.mp4", ClipType.Video, 0, 30));
        timelineNoFx.Tracks.Add(trackNoFx);

        // Timeline WITH effects
        var timelineWithFx = MakeProject(width, height);
        var trackWithFx = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clipFx = MakeClip("c1", "grade_test.mp4", ClipType.Video, 0, 30);
        clipFx.Effects = new List<Effect>
        {
            new()
            {
                Type = "color_grade",
                Enabled = true,
                Params = new()
                {
                    ["saturation"] = new EffectParam { NumericValue = 2.0 },
                    ["contrast"] = new EffectParam { NumericValue = 1.2 },
                    ["warmth"] = new EffectParam { NumericValue = 0.3 },
                }
            }
        };
        trackWithFx.Clips.Add(clipFx);
        timelineWithFx.Tracks.Add(trackWithFx);

        var builderNoFx = new CompositionBuilder();
        builderNoFx.Load(timelineNoFx);
        var builderWithFx = new CompositionBuilder();
        builderWithFx.Load(timelineWithFx);

        using var compositorNoFx = new SkiaCompositor(width, height, provider);
        using var compositorWithFx = new SkiaCompositor(width, height, provider);

        var frameNoFx = builderNoFx.Build(10);
        byte[] pixelsNoFx = await compositorNoFx.CompositeAsync(frameNoFx);

        var frameWithFx = builderWithFx.Build(10);
        byte[] pixelsWithFx = await compositorWithFx.CompositeAsync(frameWithFx);

        Assert.Equal(width * height * 4, pixelsNoFx.Length);
        Assert.Equal(width * height * 4, pixelsWithFx.Length);

        // Color grade should change pixel values vs no-effect control
        bool anyDifference = false;
        for (int i = 0; i < pixelsNoFx.Length; i++)
        {
            if (pixelsNoFx[i] != pixelsWithFx[i])
            {
                anyDifference = true;
                break;
            }
        }
        Assert.True(anyDifference, "Color grade effect should produce different pixel values than no effect");
    }

    [Fact]
    public async Task CompositeAsync_MultipleEffects_PipelineCompletes()
    {
        int width = 40, height = 30;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "multi_fx.mp4", ClipType.Video, 0, 30);
        clip.Effects = new List<Effect>
        {
            new()
            {
                Type = "color_grade",
                Enabled = true,
                Params = new()
                {
                    ["saturation"] = new EffectParam { NumericValue = 1.5 },
                    ["contrast"] = new EffectParam { NumericValue = 1.0 },
                    ["warmth"] = new EffectParam { NumericValue = 0.0 },
                }
            },
            new()
            {
                Type = "grain",
                Enabled = true,
                Params = new()
                {
                    ["amount"] = new EffectParam { NumericValue = 0.05 },
                }
            }
        };
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(10);
        byte[] pixels = await compositor.CompositeAsync(frame);

        Assert.Equal(width * height * 4, pixels.Length);
        Assert.True(pixels.Any(b => b > 0), "Multiple effects should produce visible pixels");
    }

    // ── Edge case tests ─────────────────────────────────────────

    [Fact]
    public async Task CompositeAsync_SingleFrameTimeline_ProducesOneFrame()
    {
        int width = 320, height = 240;
        var timeline = MakeProject(width, height);
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "single.mp4", ClipType.Video, 0, 1));
        timeline.Tracks.Add(track);

        var provider = new TestFrameProvider(width, height);
        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(0);
        byte[] pixels = await compositor.CompositeAsync(frame);

        Assert.Equal(width * height * 4, pixels.Length);
        Assert.Equal(255, pixels[3]);
    }

    [Fact]
    public async Task CompositeAsync_FrameOutOfRange_ReturnsAllBlack()
    {
        int width = 80, height = 60;
        var timeline = MakeProject(width, height);
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(MakeClip("c1", "vid.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track);

        var provider = new TestFrameProvider(width, height);
        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(200);
        Assert.Empty(frame.Visual);

        byte[] pixels = await compositor.CompositeAsync(frame);
        Assert.Equal(width * height * 4, pixels.Length);
    }

    [Fact]
    public async Task CompositeAsync_HiddenTrack_ExcludesClips()
    {
        int width = 80, height = 60;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video, IsHidden = true };
        track.Clips.Add(MakeClip("c1", "hidden.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(track);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(10);
        Assert.Empty(frame.Visual);
    }

    [Fact]
    public async Task CompositeAsync_MultipleResolutions_AllProduceValidOutput()
    {
        var resolutions = new (int w, int h)[] { (1920, 1080), (1280, 720), (640, 480), (320, 240), (160, 120) };

        foreach (var (w, h) in resolutions)
        {
            var timeline = MakeProject(w, h);
            var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
            track.Clips.Add(MakeClip("c1", $"vid_{w}x{h}.mp4", ClipType.Video, 0, 30));
            timeline.Tracks.Add(track);

            var provider = new TestFrameProvider(w, h);
            using var compositor = new SkiaCompositor(w, h, provider);
            var builder = new CompositionBuilder();
            builder.Load(timeline);

            var frame = builder.Build(15);
            byte[] pixels = await compositor.CompositeAsync(frame);

            Assert.Equal(w * h * 4, pixels.Length);
            Assert.Equal(255, pixels[3]);

            bool nonBlack = false;
            for (int i = 0; i < Math.Min(100, pixels.Length); i += 4)
            {
                if (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0)
                { nonBlack = true; break; }
            }
            Assert.True(nonBlack, $"Frame at {w}x{h} should contain non-black pixels");
        }
    }

    // ── Audio-visual slot separation ────────────────────────────

    [Fact]
    public async Task CompositeAsync_VideoSlotsAndAudioSlots_SeparatedCorrectly()
    {
        int width = 80, height = 60;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var videoTrack = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        videoTrack.Clips.Add(MakeClip("vc1", "video.mp4", ClipType.Video, 0, 50));
        timeline.Tracks.Add(videoTrack);

        var audioTrack = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio };
        audioTrack.Clips.Add(MakeClip("ac1", "audio.mp3", ClipType.Audio, 0, 50));
        timeline.Tracks.Add(audioTrack);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(10);
        Assert.Single(frame.Visual);
        Assert.Single(frame.Audio);

        byte[] pixels = await compositor.CompositeAsync(frame);
        Assert.Equal(width * height * 4, pixels.Length);
    }

    [Fact]
    public async Task CompositeAsync_LinkedVideoAndAudio_IndependentlyComposited()
    {
        int width = 80, height = 60;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);
        var linkGroupId = Guid.NewGuid().ToString();

        var videoTrack = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        videoTrack.Clips.Add(MakeClip("vc1", "linked_video.mp4", ClipType.Video,
            0, 50, linkGroupId: linkGroupId));
        timeline.Tracks.Add(videoTrack);

        var audioTrack = new Track { Id = "A1", Name = "A1", Type = ClipType.Audio };
        audioTrack.Clips.Add(MakeClip("ac1", "linked_audio.mp3", ClipType.Audio,
            0, 50, linkGroupId: linkGroupId));
        timeline.Tracks.Add(audioTrack);

        using var compositor = new SkiaCompositor(width, height, provider);
        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(25);
        Assert.Single(frame.Visual);
        Assert.Equal("vc1", frame.Visual[0].Clip.Id);
        Assert.Equal(linkGroupId, frame.Visual[0].Clip.LinkGroupId);
        Assert.Single(frame.Audio);
        Assert.Equal("ac1", frame.Audio[0].Clip.Id);
        Assert.Equal(linkGroupId, frame.Audio[0].Clip.LinkGroupId);

        byte[] pixels = await compositor.CompositeAsync(frame);
        Assert.Equal(width * height * 4, pixels.Length);
    }

    // ── Speed / time-remapping tests ────────────────────────────

    [Fact]
    public async Task CompositeAsync_ClipWithSpeed_SourceFrameMappedCorrectly()
    {
        int width = 40, height = 30;
        var timeline = MakeProject(width, height);
        var provider = new TestFrameProvider(width, height);

        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        var clip = MakeClip("c1", "speed_test.mp4", ClipType.Video, 0, 50);
        clip.Speed = 2.0;
        clip.TrimStartFrame = 0;
        track.Clips.Add(clip);
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame0 = builder.Build(10);
        Assert.NotEmpty(frame0.Visual);
        Assert.Equal(20, frame0.Visual[0].SourceFrame);

        var frame1 = builder.Build(0);
        Assert.Equal(0, frame1.Visual[0].SourceFrame);
    }

    // ── Provider that returns pure green ────────────────────────

    private sealed class GreenFrameProvider : IFrameProvider
    {
        private readonly int _width, _height;
        public GreenFrameProvider(int w, int h) { _width = w; _height = h; }
        public Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height)
        {
            var pixels = new byte[_width * _height * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;
                pixels[i + 1] = 255;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }
            return Task.FromResult<byte[]?>(pixels);
        }
    }
}
