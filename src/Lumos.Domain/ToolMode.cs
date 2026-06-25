namespace Lumos.Domain;

/// The active editing tool. Affects timeline click behavior and cursor.
public enum ToolMode
{
    Pointer,  // V key — default selection/move/trim
    Razor     // C key — click to split clips
}
