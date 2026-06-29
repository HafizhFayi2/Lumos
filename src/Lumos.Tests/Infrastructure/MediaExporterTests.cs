using System;
using System.Threading.Tasks;
using Lumos.Application;
using Lumos.Domain;
using Lumos.Infrastructure;
using Lumos.Media;
using Xunit;

namespace Lumos.Tests.Infrastructure;

/// <summary>
/// Tests for MediaExporter validation, export profiles, and edge case handling.
/// Does NOT run actual FFmpeg export — validates input/configuration only.
/// </summary>
public class MediaExporterTests
{
    // ── Export profile validation ───────────────────────────────

    [Fact]
    public void ExportProfile_DefaultProfile_HasExpectedValues()
    {
        var profile = new ExportProfile();

        Assert.Equal("H.264 1080p60", profile.Name);
        Assert.Equal("mp4", profile.Format);
        Assert.Equal("libx264", profile.VideoCodec);
        Assert.Equal("aac", profile.AudioCodec);
        Assert.Equal(15000, profile.VideoBitrateKbps);
        Assert.Equal(320, profile.AudioBitrateKbps);
        Assert.Equal(1920, profile.Width);
        Assert.Equal(1080, profile.Height);
        Assert.Equal(60, profile.FrameRate);
    }

    [Fact]
    public void ExportProfile_CustomProfile_HoldsAllValues()
    {
        var profile = new ExportProfile
        {
            Name = "H.265 720p30",
            Format = "mp4",
            VideoCodec = "libx265",
            AudioCodec = "libopus",
            VideoBitrateKbps = 5000,
            AudioBitrateKbps = 128,
            Width = 1280,
            Height = 720,
            FrameRate = 30,
        };

        Assert.Equal("H.265 720p30", profile.Name);
        Assert.Equal("libx265", profile.VideoCodec);
        Assert.Equal("libopus", profile.AudioCodec);
        Assert.Equal(5000, profile.VideoBitrateKbps);
        Assert.Equal(128, profile.AudioBitrateKbps);
        Assert.Equal(1280, profile.Width);
        Assert.Equal(720, profile.Height);
        Assert.Equal(30, profile.FrameRate);
    }

    [Fact]
    public void ExportProfile_ResolutionSettings_Varied()
    {
        // Test various resolution configurations
        var profiles = new[]
        {
            new ExportProfile { Width = 3840, Height = 2160, Name = "4K" },
            new ExportProfile { Width = 1920, Height = 1080, Name = "1080p" },
            new ExportProfile { Width = 1280, Height = 720,  Name = "720p" },
            new ExportProfile { Width = 854,  Height = 480,  Name = "480p" },
            new ExportProfile { Width = 640,  Height = 360,  Name = "360p" },
        };

        foreach (var p in profiles)
        {
            Assert.True(p.Width > 0, $"{p.Name}: Width must be positive");
            Assert.True(p.Height > 0, $"{p.Name}: Height must be positive");
            Assert.True(p.FrameRate > 0, $"{p.Name}: FrameRate must be positive");
        }
    }

    [Fact]
    public void ExportProfile_ExtremeResolution_IsAllowed()
    {
        var profile = new ExportProfile { Width = 7680, Height = 4320, FrameRate = 120 };
        Assert.Equal(7680, profile.Width);
        Assert.Equal(4320, profile.Height);
        Assert.Equal(120, profile.FrameRate);
    }

    // ── Timeline resolution fallback ────────────────────────────

    [Fact]
    public void Export_EmptyResolution_UsesTimelineFallback()
    {
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        var profile = new ExportProfile { Width = 0, Height = 0 }; // Resolve from timeline

        int exportWidth = profile.Width > 0 ? profile.Width : timeline.Width;
        int exportHeight = profile.Height > 0 ? profile.Height : timeline.Height;
        double fps = profile.FrameRate > 0 ? profile.FrameRate : timeline.Fps;

        Assert.Equal(1920, exportWidth);
        Assert.Equal(1080, exportHeight);
        Assert.Equal(60, fps); // Default profile fps
    }

    [Fact]
    public void Export_PartialResolution_BlendsProfileAndTimeline()
    {
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        var profile = new ExportProfile { Width = 1280, Height = 0, FrameRate = 0 };

        int exportWidth = profile.Width > 0 ? profile.Width : timeline.Width;
        int exportHeight = profile.Height > 0 ? profile.Height : timeline.Height;
        double fps = profile.FrameRate > 0 ? profile.FrameRate : timeline.Fps;

        Assert.Equal(1280, exportWidth);  // From profile
        Assert.Equal(1080, exportHeight); // From timeline fallback
        Assert.Equal(30, fps);            // From timeline fallback
    }

    // ── Codec configuration ─────────────────────────────────────

    [Fact]
    public void ExportProfile_VariedCodecs_Accepted()
    {
        var configs = new[]
        {
            new ExportProfile { VideoCodec = "libx264", AudioCodec = "aac" },
            new ExportProfile { VideoCodec = "libx265", AudioCodec = "libopus" },
            new ExportProfile { VideoCodec = "libvpx-vp9", AudioCodec = "libvorbis" },
            new ExportProfile { VideoCodec = "h264_nvenc", AudioCodec = "aac" },
            new ExportProfile { VideoCodec = "libsvtav1", AudioCodec = "libopus" },
        };

        foreach (var p in configs)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.VideoCodec),
                $"Video codec must be specified");
            Assert.False(string.IsNullOrWhiteSpace(p.AudioCodec),
                $"Audio codec must be specified for {p.VideoCodec}");
        }
    }

    [Fact]
    public void ExportProfile_BitrateConfigurations_Valid()
    {
        var configs = new[]
        {
            new ExportProfile { VideoBitrateKbps = 50000, AudioBitrateKbps = 512 }, // High quality
            new ExportProfile { VideoBitrateKbps = 1000, AudioBitrateKbps = 64 },   // Low quality
            new ExportProfile { VideoBitrateKbps = 8000, AudioBitrateKbps = 192 },  // Medium quality
            new ExportProfile { VideoBitrateKbps = 0, AudioBitrateKbps = 0 },       // Codec default
        };

        foreach (var p in configs)
        {
            Assert.True(p.VideoBitrateKbps >= 0,
                $"Video bitrate must be non-negative ({p.VideoBitrateKbps})");
            Assert.True(p.AudioBitrateKbps >= 0,
                $"Audio bitrate must be non-negative ({p.AudioBitrateKbps})");
        }
    }

    // ── Empty timeline ──────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_EmptyTimeline_Throws()
    {
        var exporter = new MediaExporter();
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        var profile = new ExportProfile();

        // TotalFrames = 0 since no clips
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            exporter.ExportAsync(timeline, profile, "output.mp4",
                new Progress<double>()));
    }

    [Fact]
    public void MediaExporter_ImplementsInterface()
    {
        var exporter = new MediaExporter();
        Assert.IsAssignableFrom<IMediaExporter>(exporter);
    }

    // ── Timeline total frames ───────────────────────────────────

    [Fact]
    public void Timeline_WithClips_HasCorrectTotalFrames()
    {
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        timeline.Tracks.Add(new Track
        {
            Id = "V1",
            Name = "V1",
            Type = ClipType.Video,
            Clips = { new Clip
            {
                Id = "c1",
                MediaRef = "test.mp4",
                MediaType = ClipType.Video,
                StartFrame = 0,
                DurationFrames = 150,
            }}
        });

        // TotalFrames is computed from the max clip end frame
        Assert.Equal(150, timeline.TotalFrames);
    }

    [Fact]
    public void Timeline_MultipleTracks_TotalFramesIsMaxEnd()
    {
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        timeline.Tracks.Add(new Track
        {
            Id = "V1",
            Name = "V1",
            Type = ClipType.Video,
            Clips = { new Clip
            {
                Id = "c1",
                MediaRef = "short.mp4",
                MediaType = ClipType.Video,
                StartFrame = 0,
                DurationFrames = 50,
            }}
        });
        timeline.Tracks.Add(new Track
        {
            Id = "A1",
            Name = "A1",
            Type = ClipType.Audio,
            Clips = { new Clip
            {
                Id = "ac1",
                MediaRef = "long.mp3",
                MediaType = ClipType.Audio,
                StartFrame = 0,
                DurationFrames = 200,
            }}
        });

        Assert.Equal(200, timeline.TotalFrames); // Max across tracks
    }

    [Fact]
    public void Timeline_EmptyTracks_TotalFramesIsZero()
    {
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        timeline.Tracks.Add(new Track { Id = "V1", Name = "V1", Type = ClipType.Video });

        Assert.Equal(0, timeline.TotalFrames);
    }

    // ── CompositionFrame validation ─────────────────────────────

    [Fact]
    public void CompositionFrame_Empty_HasNoSlots()
    {
        var empty = CompositionFrame.Empty;

        Assert.Equal(0, empty.TimelineFrame);
        Assert.Empty(empty.Visual);
        Assert.Empty(empty.Audio);
    }

    [Fact]
    public void CompositionFrame_WithSlots_StoresCorrectly()
    {
        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        var track = new Track { Id = "V1", Name = "V1", Type = ClipType.Video };
        track.Clips.Add(new Clip
        {
            Id = "c1",
            MediaRef = "test.mp4",
            MediaType = ClipType.Video,
            StartFrame = 0,
            DurationFrames = 30,
        });
        timeline.Tracks.Add(track);

        var builder = new CompositionBuilder();
        builder.Load(timeline);

        var frame = builder.Build(15);
        Assert.Equal(15, frame.TimelineFrame);
        Assert.Single(frame.Visual);
        Assert.Empty(frame.Audio);
    }
}
