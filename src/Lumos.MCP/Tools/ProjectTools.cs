using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.MCP.Tools;

[McpServerToolType]
public sealed class ProjectTools
{
    private readonly EditorStore _store;

    public ProjectTools(EditorStore store)
    {
        _store = store;
    }

    [McpServerTool(Name = ToolDefinitions.SetProjectSettings)]
    [Description("Update project settings. Args: name (string, optional), width (int, optional), height (int, optional), fps (int, optional).")]
    public Task<string> SetProjectSettingsAsync(JsonElement args, CancellationToken ct = default)
    {
        var state = _store.State;
        var tl = state.Timeline.Timeline;
        if (tl == null)
            return Task.FromResult(McpToolHelpers.Error("No timeline loaded."));

        McpToolHelpers.TryGetString(args, "name", out var name);
        McpToolHelpers.TryGetInt(args, "width", out var width);
        McpToolHelpers.TryGetInt(args, "height", out var height);
        McpToolHelpers.TryGetInt(args, "fps", out var fps);

        // Validate ranges
        if (width > 0 && (width < 16 || width > 7680))
            return Task.FromResult(McpToolHelpers.Error($"width ({width}) must be between 16 and 7680"));

        if (height > 0 && (height < 16 || height > 4320))
            return Task.FromResult(McpToolHelpers.Error($"height ({height}) must be between 16 and 4320"));

        if (fps > 0 && (fps < 1 || fps > 240))
            return Task.FromResult(McpToolHelpers.Error($"fps ({fps}) must be between 1 and 240"));

        // Update project name outside timeline mutation to avoid nested lock
        if (!string.IsNullOrWhiteSpace(name))
        {
            _store.Dispatch(s => (s with { ProjectName = name }, StateField.All));
        }

        _store.MutateTimeline("Update project settings", t =>
        {
            if (width > 0) t.Width = width;
            if (height > 0) t.Height = height;
            if (fps > 0) t.Fps = fps;
            t.SettingsConfigured = true;
        });

        var updatedTl = _store.State.Timeline.Timeline;
        var result = new
        {
            projectName = _store.State.ProjectName,
            width = updatedTl.Width,
            height = updatedTl.Height,
            fps = updatedTl.Fps,
        };

        return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        }));
    }
}
