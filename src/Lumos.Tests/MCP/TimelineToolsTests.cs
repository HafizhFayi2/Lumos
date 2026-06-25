using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;
using Lumos.MCP.Tools;

namespace Lumos.Tests.MCP;

public class TimelineToolsTests
{
    [Fact]
    public async Task InspectTimelineAsync_ReturnsValidJsonSnapshot()
    {
        var store = new EditorStore();
        var queue = new CommandQueue(store);
        var tools = new TimelineTools(store, queue);

        var timeline = new Timeline { Fps = 30, Width = 1920, Height = 1080 };
        timeline.Tracks.Add(new Track { Id = "V1", Name = "V1", Type = ClipType.Video });
        timeline.Tracks[0].Clips.Add(new Clip
        {
            Id = "clip123",
            MediaRef = "video.mp4",
            MediaType = ClipType.Video,
            StartFrame = 0,
            DurationFrames = 150
        });

        store.SetProject(Guid.NewGuid(), "Test Project", timeline);

        var result = await tools.InspectTimelineAsync();
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Equal(30, root.GetProperty("fps").GetInt32());
        Assert.Equal(1920, root.GetProperty("width").GetInt32());
        Assert.Equal(1080, root.GetProperty("height").GetInt32());
        Assert.Equal(150, root.GetProperty("totalFrames").GetInt32());

        var tracks = root.GetProperty("tracks");
        Assert.Equal(1, tracks.GetArrayLength());
        var track = tracks[0];
        Assert.Equal("V1", track.GetProperty("id").GetString());

        var clips = track.GetProperty("clips");
        Assert.Equal(1, clips.GetArrayLength());
        var clip = clips[0];
        Assert.Equal("clip123", clip.GetProperty("id").GetString());
        Assert.Equal("video.mp4", clip.GetProperty("mediaRef").GetString());
    }

    [Fact]
    public async Task SplitClipAsync_InvalidArgs_ReturnsError()
    {
        var store = new EditorStore();
        var queue = new CommandQueue(store);
        var tools = new TimelineTools(store, queue);

        var args = JsonDocument.Parse("{}").RootElement;
        var result = await tools.SplitClipAsync(args);
        Assert.Contains("error", result);
    }
}
