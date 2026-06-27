using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Lumos.Application;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class ExportTools
{
    private readonly EditorStore _store;
    private readonly IMediaExporter _exporter;

    public ExportTools(EditorStore store, IMediaExporter exporter)
    {
        _store    = store;
        _exporter = exporter;
    }

    [McpServerTool(Name = ToolDefinitions.ExportVideo)]
    [Description("Render and export the timeline to an MP4 file. Args: output_path (string), width (int, default 1920), height (int, default 1080), fps (int, default 30), video_bitrate_kbps (int, default 8000).")]
    public async Task<string> ExportVideoAsync(JsonElement args, CancellationToken ct = default)
    {
        if (!McpToolHelpers.TryGetString(args, "output_path", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
            return McpToolHelpers.Error("output_path (string) is required");

        McpToolHelpers.TryGetInt(args, "width",              out var width);
        McpToolHelpers.TryGetInt(args, "height",             out var height);
        McpToolHelpers.TryGetInt(args, "fps",                out var fps);
        McpToolHelpers.TryGetInt(args, "video_bitrate_kbps", out var bitrate);

        if (width > 0 && (width < 16 || width > 7680))
            return McpToolHelpers.Error($"width ({width}) must be between 16 and 7680");
        if (height > 0 && (height < 16 || height > 4320))
            return McpToolHelpers.Error($"height ({height}) must be between 16 and 4320");
        if (fps > 0 && (fps < 1 || fps > 240))
            return McpToolHelpers.Error($"fps ({fps}) must be between 1 and 240");
        if (bitrate > 0 && (bitrate < 100 || bitrate > 100_000))
            return McpToolHelpers.Error($"video_bitrate_kbps ({bitrate}) must be between 100 and 100,000");

        var tl = _store.State.Timeline.Timeline;
        if (tl == null)
            return McpToolHelpers.Error("No timeline loaded.");
        if (tl.TotalFrames == 0)
            return McpToolHelpers.Error("Timeline is empty — nothing to export.");

        var profile = new ExportProfile
        {
            Width             = width  > 0 ? width  : tl.Width,
            Height            = height > 0 ? height : tl.Height,
            FrameRate         = fps    > 0 ? fps    : tl.Fps,
            VideoBitrateKbps  = bitrate > 0 ? bitrate : 8000,
            VideoCodec        = "libx264",
        };

        try
        {
            var progress = new Progress<double>(_ => { });
            await _exporter.ExportAsync(tl, profile, outputPath!, progress);
            return McpToolHelpers.Ok(new { output_path = outputPath, width = profile.Width, height = profile.Height, fps = profile.FrameRate });
        }
        catch (Exception ex)
        {
            return McpToolHelpers.Error($"Export failed: {ex.Message}");
        }
    }
}
