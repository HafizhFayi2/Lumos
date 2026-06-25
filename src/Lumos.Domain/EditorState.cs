namespace Lumos.Domain;

/// Legacy mutable editor state. Retained for TimelineInputController compatibility
/// during migration. New code should use Lumos.Application.State.EditorState.
public class LegacyEditorState
{
    public TimeSpan PlayheadPosition { get; set; }
    public Guid ActiveTimelineId { get; set; }
    public List<Guid> SelectedClipIds { get; set; } = new();
    public Guid? ActiveTrackId { get; set; }
    public bool IsPlaying { get; set; }
}
