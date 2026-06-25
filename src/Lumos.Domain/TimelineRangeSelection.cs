namespace Lumos.Domain;

public readonly record struct TimelineRangeSelection(int StartFrame, int EndFrame)
{
    public TimelineRangeSelection Normalized => StartFrame <= EndFrame
        ? this
        : new TimelineRangeSelection(EndFrame, StartFrame);

    public bool IsValid
    {
        get
        {
            var norm = Normalized;
            return norm.EndFrame > norm.StartFrame;
        }
    }

    public bool Contains(int frame)
    {
        var norm = Normalized;
        return frame >= norm.StartFrame && frame < norm.EndFrame;
    }
}
