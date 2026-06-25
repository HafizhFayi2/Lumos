using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Lumos.Application;
using Lumos.Application.Assets;
using Lumos.Application.Commands;
using Lumos.Application.State;
using Lumos.Domain;
using Lumos.MCP;
using Lumos.MCP.Tools;

namespace Lumos.Tests.MCP;

public class McpDispatchTests
{
    private sealed class NullThumbnailGenerator : IThumbnailGenerator
    {
        public Task<string> GenerateThumbnailAsync(Asset asset, TimeSpan position)
            => Task.FromResult(string.Empty);

        public Task<List<string>> GenerateWaveformAsync(Asset asset)
            => Task.FromResult(new List<string>());
    }

    [Fact]
    public void ToolDefinitions_AllConstantsAreUnique()
    {
        var names = new[]
        {
            ToolDefinitions.InspectTimeline,
            ToolDefinitions.SplitClip,
            ToolDefinitions.TrimClip,
            ToolDefinitions.MoveClip,
            ToolDefinitions.RemoveClips,
            ToolDefinitions.RippleDelete,
            ToolDefinitions.ListAssets,
            ToolDefinitions.ImportMedia,
            ToolDefinitions.ExportVideo,
            ToolDefinitions.GenerateCaptions,
        };
        var set = new System.Collections.Generic.HashSet<string>(names);
        Assert.Equal(names.Length, set.Count);
    }

    [Fact]
    public async Task TimelineTools_InspectTimeline_EmptyTimeline_ReturnsZeroTotalFrames()
    {
        var store = new EditorStore();
        var queue = new CommandQueue(store);
        var tools = new TimelineTools(store, queue);

        var timeline = new Timeline { Fps = 24, Width = 1920, Height = 1080 };
        store.SetProject(Guid.NewGuid(), "Dispatch Test", timeline);

        var result = await tools.InspectTimelineAsync();
        using var doc = JsonDocument.Parse(result);
        Assert.Equal(24, doc.RootElement.GetProperty("fps").GetInt32());
        Assert.Equal(0, doc.RootElement.GetProperty("totalFrames").GetInt32());
    }

    [Fact]
    public async Task CaptionTools_MissingArg_ReturnsError()
    {
        var store = new EditorStore();
        var queue = new CommandQueue(store);
        var tg = new NullThumbnailGenerator();
        var assets = new AssetManager(tg);
        var tools = new CaptionTools(store, queue, assets);

        var args = JsonDocument.Parse("{}").RootElement;
        var result = await tools.GenerateCaptionsAsync(args);
        Assert.Contains("error", result);
    }
}
