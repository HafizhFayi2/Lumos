namespace Palmier.Domain;

public enum LayoutPreset
{
    Default,
    Media,
    Vertical
}

public static class Layout
{
    public const double MediaPanelDefault = 500;
    public const double MediaPanelMin = 280;

    public const double InspectorDefault = 260;
    public const double InspectorMin = 150;

    public const double AgentPanelMin = 240;
    public const double AgentPanelMax = 640;
    public const double ChatColumnMax = 640;

    public const double PanelHeaderHeight = 28;
    public const double ToolbarHeight = 38;

    public const double PanelGap = 5;

    // Timeline
    public const double TimelineMinHeight = 100;
    public const double TimelineMaxHeight = 700;
    public const double TrackHeight = 50;
    public const double RulerHeight = 24;
    public const double TrackHeaderWidth = 100;
    public const double DropZoneHeight = 60;
    public const double InsertThreshold = 10;
    public const double DragThreshold = 3;

    // Preview
    public const double PreviewMinWidth = 400;
    public const double PreviewMinHeight = 320;
}

public static class Defaults
{
    public const double PixelsPerFrame = 4.0;
    public const double ImageDurationSeconds = 5.0;
    public const double AudioTTSDurationSeconds = 10.0;
    public const double AudioMusicDurationSeconds = 60.0;
    public const double TextDurationSeconds = 3.0;
    public const double AspectTolerance = 0.02;
}

public static class Snap
{
    public const double ThresholdPixels = 8.0;
    public const double StickyMultiplier = 1.5;
    public const double PlayheadMultiplier = 1.5;
}

public static class TrackSize
{
    public const double MinHeight = 32;
    public const double MaxHeight = 200;
    public const double ResizeHandleZone = 6;
}

public static class Zoom
{
    public const double Min = 0.05;
    public const double Floor = 0.0001;
    public const double Max = 40.0;
    public const double ToolbarStepFactor = 1.25;
    public const double ScrollSensitivity = 0.04;
    public const double MagnifySensitivity = 1.5;
    public const double PanSpeed = 5.0;
    public const double FitAllBuffer = 3.0;
}

public static class TimelineAutoScroll
{
    public const double EdgeZoneWidth = 56;
    public const double MaxZoneFraction = 0.5;
    public const double MinStep = 4;
    public const double MaxStep = 28;
    public const double Interval = 1.0 / 60.0;
}

public static class Trim
{
    public const double HandleWidth = 4.0;
    public const double ClipCornerRadius = 3.0;
}

public static class ProjectDefaults
{
    public const string FileExtension = "palmier";
    public const string RegistryFilename = "project-registry.json";
    public const string TypeIdentifier = "io.palmier.project";
    public const string DefaultProjectName = "Untitled Project";
    public const string TimelineFilename = "project.json";
    public const string ManifestFilename = "media.json";
    public const string GenerationLogFilename = "generation-log.json";
    public const string ThumbnailFilename = "thumbnail.jpg";
    public const string MediaDirectoryName = "media";
}
