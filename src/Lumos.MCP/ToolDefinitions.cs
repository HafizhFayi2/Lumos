namespace Lumos.MCP;

/// Central registry of all tool names exposed over MCP.
/// Tool descriptions are intentionally terse — they are sent to the model on every call.
public static class ToolDefinitions
{
    // ── Timeline tools ───────────────────────────────────────────────────────
    public const string InspectTimeline    = "inspect_timeline";
    public const string GetTimeline        = "get_timeline";
    public const string SplitClip          = "split_clip";
    public const string TrimClip           = "trim_clip";
    public const string MoveClip           = "move_clip";
    public const string RemoveClips        = "remove_clips";
    public const string RippleDelete       = "ripple_delete";
    public const string RippleDeleteRanges = "ripple_delete_ranges";
    public const string AddClips           = "add_clips";
    public const string InsertClips        = "insert_clips";
    public const string ApplyEffect        = "apply_effect";

    // ── Media/asset tools ────────────────────────────────────────────────────
    public const string ListAssets         = "list_assets";
    public const string GetMedia           = "get_media";
    public const string InspectMedia       = "inspect_media";
    public const string ImportMedia        = "import_media";
    public const string DeleteMedia        = "delete_media";

    // ── Export tools ─────────────────────────────────────────────────────────
    public const string ExportVideo        = "export_video";

    // ── Caption tools ────────────────────────────────────────────────────────
    public const string GenerateCaptions   = "generate_captions";

    // ── Project tools ────────────────────────────────────────────────────────
    public const string SetProjectSettings = "set_project_settings";

    // ── Media folder tools ───────────────────────────────────────────────────
    public const string ListMediaFolders   = "list_media_folders";
    public const string CreateMediaFolder  = "create_media_folder";
    public const string RenameMediaFolder  = "rename_media_folder";
    public const string MoveMedia          = "move_media";
    public const string DeleteMediaFolder  = "delete_media_folder";

    // ── AI/editing workflow tools ────────────────────────────────────────────
    public const string GetTranscript      = "get_transcript";
    public const string SearchTranscript   = "search_transcript";
    public const string DetectFillerWords  = "detect_filler_words";
    public const string RemoveFillerRegions = "remove_filler_regions";
    public const string DetectHighlights   = "detect_highlights";
    public const string InspectFrame       = "inspect_frame";
    public const string GetActionHistory   = "get_action_history";
    public const string AnalyzeSilences      = "analyze_silences";

    // ── Generative media tools ──────────────────────────────────────────────
    public const string ListModels           = "list_models";
    public const string GenerateMedia        = "generate_media";
    public const string GetGenerationStatus  = "get_generation_status";
    public const string GetGenerationLog     = "get_generation_log";
    public const string SetModelApiKey       = "set_model_api_key";

    public static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>
        {
            // Timeline
            [InspectTimeline]    = "Return a detailed JSON snapshot of the current timeline: tracks, clips, durations, fps.",
            [GetTimeline]        = "Return a concise summary of the current timeline: fps, resolution, total frames, track count, clip count.",
            [SplitClip]          = "Split a clip at a given frame, producing two clips in place.",
            [TrimClip]           = "Adjust a clip's in or out point by a number of frames.",
            [MoveClip]           = "Move a clip to a different start frame and/or track index.",
            [RemoveClips]        = "Remove one or more clips by ID.",
            [RippleDelete]       = "Remove a clip and shift all downstream clips left to close the gap.",
            [RippleDeleteRanges] = "Remove all clips in a frame range and shift remaining clips left to close gaps. Args: track_id (string), start_frame (int), end_frame (int).",
            [AddClips]           = "Add existing timeline clips to a track by clip ID. Args: clip_ids (array of strings), track_id (string), start_frame (int).",
            [InsertClips]        = "Insert clips at a frame position, pushing existing clips right with ripple. Args: clip_ids (array of strings), track_id (string), insert_frame (int).",
            [ApplyEffect]        = "Apply an effect to a specific clip. Args: clip_id (string), effect_type (string, e.g. color_grade, glow, clarity, vignette).",

            // Media
            [ListAssets]         = "List all media assets referenced by clips in the current project.",
            [GetMedia]           = "List all media assets in the project with full metadata (id, path, type, duration, resolution, fps).",
            [InspectMedia]       = "Return metadata for a specific media asset by ID. Args: asset_id (string).",
            [ImportMedia]        = "Add a media file to the timeline on a new or existing track.",
            [DeleteMedia]        = "Remove a media asset from the project. Args: asset_id (string).",

            // Export
            [ExportVideo]        = "Render and export the timeline to an MP4 file.",

            // Captions
            [GenerateCaptions]   = "Parse an SRT file alongside a video clip and add subtitle clips to the A2 track. Args: source_clip_id (string).",

            // Project
            [SetProjectSettings] = "Update project settings. Args: name (string, optional), width (int, optional), height (int, optional), fps (int, optional).",

            // Media folders
            [ListMediaFolders]   = "List all media folders in the project.",
            [CreateMediaFolder]  = "Create a new media folder. Args: name (string), parent_folder_id (string, optional).",
            [RenameMediaFolder]  = "Rename a media folder. Args: folder_id (string), name (string).",
            [MoveMedia]          = "Move a media asset into a folder. Args: asset_id (string), folder_id (string, or null to place at root).",
            [DeleteMediaFolder]  = "Delete a media folder. Assets in the folder will be moved to root. Args: folder_id (string).",

            // AI-assisted editing
            [GetTranscript]       = "Get the transcript for a clip or track. Args: clip_id (string, optional), track_id (string, optional). Returns transcript segments with timing and text.",
            [SearchTranscript]    = "Search transcript text for a query across all clips. Args: query (string). Returns matching segments.",
            [DetectFillerWords]   = "Detect filler words (um, uh, like) in all clips' transcripts.",
            [RemoveFillerRegions] = "Remove regions containing filler words from clips. Args: clip_id (string, optional).",
            [DetectHighlights]    = "Detect highlight-worthy regions based on transcript keywords and clip metadata. Args: top_count (int, default 5).",
            [InspectFrame]        = "Get pixel/color analysis for a specific timeline frame. Args: frame (int), width (int, optional, default 320), height (int, optional, default 180).",
            [GetActionHistory]    = "Get recent agent action history for verification. Args: count (int, default 10).",
            [AnalyzeSilences]      = "Analyze audio tracks for silence regions. Args: threshold_db (double, default -40), min_duration_frames (int, default 15).",

            // Generative media
            [ListModels]            = "List available AI generation models. Args: type (string, optional — 'image', 'video', 'audio'). Returns model IDs and providers.",
            [GenerateMedia]         = "Generate media from a text prompt. Args: prompt (string), model_id (string), negative_prompt (string, optional), width (int, optional), height (int, optional), duration_seconds (int, optional). Returns job_id for status polling.",
            [GetGenerationStatus]   = "Get the status of a generation job. Args: job_id (string). Returns status, progress, asset_id when complete.",
            [GetGenerationLog]      = "Get the generation history log for the current project.",
            [SetModelApiKey]        = "Configure an API key for a generation model (BYOK). Args: model_id (string), api_key (string).",
        };
}
