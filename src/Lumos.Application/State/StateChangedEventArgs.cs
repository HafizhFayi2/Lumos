using System;

namespace Lumos.Application.State;

/// Raised when any part of the editor state changes.
public sealed class StateChangedEventArgs : EventArgs
{
    public StateField ChangedField { get; }

    public StateChangedEventArgs(StateField changedField) => ChangedField = changedField;
}

[Flags]
public enum StateField
{
    None       = 0,
    Timeline   = 1 << 0,
    Selection  = 1 << 1,
    Viewport   = 1 << 2,
    Playback   = 1 << 3,
    History    = 1 << 4,
    ToolMode   = 1 << 5,
    DragState  = 1 << 6,
    PreviewTab  = 1 << 7,
    PreviewQuality = 1 << 8,
    All        = Timeline | Selection | Viewport | Playback | History | ToolMode | DragState | PreviewTab | PreviewQuality
}
