using Lumos.Domain;
using Xunit;

namespace Lumos.Tests.Domain;

public sealed class TimelineGeometryTests
{
    // Standard geometry: 4px/frame, 100px header, two 50px tracks
    private static TimelineGeometry MakeGeometry(double ppf = 4.0, double headerWidth = 100.0) =>
        new(ppf, headerWidth, new List<double> { 50.0, 50.0 });

    [Fact]
    public void GetXForFrame_ReturnsHeaderWidthPlusFrameOffset()
    {
        var geo = MakeGeometry(ppf: 4.0, headerWidth: 100.0);
        Assert.Equal(100.0 + 10 * 4.0, geo.GetXForFrame(10));
    }

    [Fact]
    public void GetFrameAt_RoundTripsWithGetXForFrame()
    {
        var geo = MakeGeometry(ppf: 4.0, headerWidth: 100.0);
        int frame = 25;
        double x = geo.GetXForFrame(frame);
        Assert.Equal(frame, geo.GetFrameAt(x));
    }

    [Fact]
    public void GetFrameAt_ClampedToZeroForPositionsBeforeHeader()
    {
        var geo = MakeGeometry(ppf: 4.0, headerWidth: 100.0);
        Assert.Equal(0, geo.GetFrameAt(50.0)); // before header
    }

    [Fact]
    public void GetClipRect_WidthEqualsDurationTimesPixelsPerFrame()
    {
        var geo = MakeGeometry(ppf: 4.0, headerWidth: 100.0);
        var clip = new Clip { StartFrame = 5, DurationFrames = 20 };
        var rect = geo.GetClipRect(clip, trackIndex: 0);

        Assert.Equal(20 * 4.0, rect.Width);
    }

    [Fact]
    public void GetClipRect_XEqualsHeaderPlusStartFrameTimesPixelsPerFrame()
    {
        var geo = MakeGeometry(ppf: 4.0, headerWidth: 100.0);
        var clip = new Clip { StartFrame = 10, DurationFrames = 5 };
        var rect = geo.GetClipRect(clip, trackIndex: 0);

        Assert.Equal(100.0 + 10 * 4.0, rect.X);
    }

    [Fact]
    public void GetTrackAt_ReturnsCorrectTrackIndex()
    {
        var geo = MakeGeometry();
        // Track 0 starts at RulerHeight+DropZoneHeight = 24+60=84
        double track0Y = geo.GetTrackY(0) + 1;
        Assert.Equal(0, geo.GetTrackAt(track0Y));

        double track1Y = geo.GetTrackY(1) + 1;
        Assert.Equal(1, geo.GetTrackAt(track1Y));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(30, 150)]
    [InlineData(100, 500)]
    public void GetXForFrame_ScalesByPixelsPerFrame(int frame, double expectedOffset)
    {
        var geo = new TimelineGeometry(5.0, 0.0, new List<double> { 50.0 });
        Assert.Equal(expectedOffset, geo.GetXForFrame(frame));
    }
}
