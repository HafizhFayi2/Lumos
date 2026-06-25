namespace Lumos.MCP;

/// Central registry of all tool names exposed over MCP.
/// Tool descriptions are intentionally terse — they are sent to the model on every call.
public static class ToolDefinitions
{
    public const string InspectTimeline  = "inspect_timeline";
    public const string SplitClip        = "split_clip";
    public const string TrimClip         = "trim_clip";
    public const string MoveClip         = "move_clip";
    public const string RemoveClips      = "remove_clips";
    public const string RippleDelete     = "ripple_delete";
    public const string ListAssets       = "list_assets";
    public const string ImportMedia      = "import_media";
    public const string ExportVideo      = "export_video";
    public const string GenerateCaptions = "generate_captions";

    public static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>
        {
            [InspectTimeline]  = "Return a JSON snapshot of the current timeline: tracks, clips, durations, fps.",
            [SplitClip]        = "Split a clip at a given frame, producing two clips in place.",
            [TrimClip]         = "Adjust a clip's in or out point by a number of frames.",
            [MoveClip]         = "Move a clip to a different start frame and/or track index.",
            [RemoveClips]      = "Remove one or more clips by ID.",
            [RippleDelete]     = "Remove a clip and shift all downstream clips left to close the gap.",
            [ListAssets]       = "List all media assets in the current project.",
            [ImportMedia]      = "Add a media file to the timeline on a new or existing track.",
            [ExportVideo]      = "Render and export the timeline to an MP4 file.",
            [GenerateCaptions] = "Parse an SRT file alongside a video clip and add subtitle clips to the A2 track. Args: source_clip_id (string).",
        };
}
