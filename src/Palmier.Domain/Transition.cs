namespace Palmier.Domain;

public enum TransitionType { CrossDissolve, DipToBlack, DipToWhite }

public class Transition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromClipId { get; set; }
    public Guid ToClipId { get; set; }
    public TransitionType Type { get; set; } = TransitionType.CrossDissolve;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);
}
