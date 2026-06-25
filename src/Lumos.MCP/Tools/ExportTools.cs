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
        if (!TryGetString(args, "output_path", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
            return Error("export_video requires output_path (string)");

        TryGetInt(args, "width",              out var width);
        TryGetInt(args, "height",             out var height);
        TryGetInt(args, "fps",                out var fps);
        TryGetInt(args, "video_bitrate_kbps", out var bitrate);

        var tl = _store.State.Timeline.Timeline;
        if (tl.TotalFrames == 0)
            return Error("Timeline is empty — nothing to export.");

        var profile = new ExportProfile
        {
            Width             = width  > 0 ? width  : tl.Width,
            Height            = height > 0 ? height : tl.Height,
            FrameRate         = fps    > 0 ? fps    : tl.Fps,
            VideoBitrateKbps  = bitrate > 0 ? bitrate : 8000,
            VideoCodec        = "libx264",
        };

        var progressLog = new List<double>();
        var progress = new Progress<double>(p => progressLog.Add(p));

        try
        {
            await _exporter.ExportAsync(tl, profile, outputPath!, progress);
            return JsonSerializer.Serialize(new { ok = true, output_path = outputPath });
        }
        catch (Exception ex)
        {
            return Error($"Export failed: {ex.Message}");
        }
    }

    private static string Error(string msg) =>
        JsonSerializer.Serialize(new { ok = false, error = msg });

    private static bool TryGetString(JsonElement el, string key, out string? value)
    {
        value = null;
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
        { value = p.GetString(); return true; }
        return false;
    }

    private static bool TryGetInt(JsonElement el, string key, out int value)
    {
        value = 0;
        if (el.TryGetProperty(key, out var p) && p.TryGetInt32(out value)) return true;
        return false;
    }
}
