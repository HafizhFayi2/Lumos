using System.Text.Json.Serialization;

namespace Palmier.Domain;

/// Canvas-normalized (0-1) transform. Center coordinates and size.
public sealed class Transform : IEquatable<Transform>
{
    public double CenterX { get; set; } = 0.5;
    public double CenterY { get; set; } = 0.5;
    public double Width   { get; set; } = 1.0;
    public double Height  { get; set; } = 1.0;
    public double Rotation { get; set; } = 0; // degrees, clockwise
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical   { get; set; }

    [JsonIgnore] public (double X, double Y) TopLeft => (CenterX - Width / 2, CenterY - Height / 2);
    [JsonIgnore] public (double X, double Y) Center  => (CenterX, CenterY);

    public Transform() { }

    public Transform(double centerX, double centerY, double width, double height,
        double rotation = 0, bool flipH = false, bool flipV = false)
    {
        CenterX = centerX; CenterY = centerY;
        Width = width; Height = height;
        Rotation = rotation; FlipHorizontal = flipH; FlipVertical = flipV;
    }

    public static Transform FromTopLeft(double x, double y, double w, double h) =>
        new(x + w / 2, y + h / 2, w, h);

    /// Snap value to canvas boundary (0 or 1) within threshold.
    public static double SnapToBoundary(double value, double threshold)
    {
        if (Math.Abs(value) < threshold) return 0;
        if (Math.Abs(value - 1) < threshold) return 1;
        return value;
    }

    public void SnapToCanvasEdges(double threshold)
    {
        var tl = TopLeft;
        double snappedLeft  = SnapToBoundary(tl.X, threshold);
        double snappedRight = SnapToBoundary(tl.X + Width, threshold);
        if (snappedLeft != tl.X)
            CenterX -= tl.X - snappedLeft;
        else if (snappedRight != tl.X + Width)
            CenterX -= tl.X + Width - snappedRight;

        var tl2 = TopLeft;
        double snappedTop    = SnapToBoundary(tl2.Y, threshold);
        double snappedBottom = SnapToBoundary(tl2.Y + Height, threshold);
        if (snappedTop != tl2.Y)
            CenterY -= tl2.Y - snappedTop;
        else if (snappedBottom != tl2.Y + Height)
            CenterY -= tl2.Y + Height - snappedBottom;
    }

    public (bool X, bool Y) SnapCenterToCanvasCenter(double thresholdH, double thresholdV)
    {
        bool snappedX = false, snappedY = false;
        if (Math.Abs(CenterX - 0.5) < thresholdH) { CenterX = 0.5; snappedX = true; }
        if (Math.Abs(CenterY - 0.5) < thresholdV) { CenterY = 0.5; snappedY = true; }
        return (snappedX, snappedY);
    }

    public Transform Clone() => new(CenterX, CenterY, Width, Height, Rotation, FlipHorizontal, FlipVertical);

    public bool Equals(Transform? other) =>
        other is not null &&
        CenterX == other.CenterX && CenterY == other.CenterY &&
        Width == other.Width && Height == other.Height &&
        Rotation == other.Rotation &&
        FlipHorizontal == other.FlipHorizontal && FlipVertical == other.FlipVertical;

    public override bool Equals(object? obj) => Equals(obj as Transform);
    public override int GetHashCode() =>
        HashCode.Combine(CenterX, CenterY, Width, Height, Rotation, FlipHorizontal, FlipVertical);
}
