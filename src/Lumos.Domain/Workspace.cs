namespace Palmier.Domain;

public class Workspace
{
    public double LeftPanelWidth { get; set; } = 280;
    public double RightPanelWidth { get; set; } = 300;
    public double TimelineHeight { get; set; } = 300;
    public bool IsAiPanelVisible { get; set; } = false;
    public bool IsInspectorVisible { get; set; } = true;
}
