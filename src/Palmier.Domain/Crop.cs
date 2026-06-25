namespace Palmier.Domain;

/// Per-clip crop as edge insets in normalized (0-1) source coordinates.
public sealed class Crop : IEquatable<Crop>, IKeyframeInterpolatable<Crop>
{
    public double Left   { get; set; }
    public double Top    { get; set; }
    public double Right  { get; set; }
    public double Bottom { get; set; }

    public bool IsIdentity => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
    public double VisibleWidthFraction  => Math.Max(0, 1 - Left - Right);
    public double VisibleHeightFraction => Math.Max(0, 1 - Top - Bottom);

    public Crop() { }
    public Crop(double left, double top, double right, double bottom)
    {
        Left = left; Top = top; Right = right; Bottom = bottom;
    }

    public static Crop Interpolate(Crop a, Crop b, double t) => new(
        a.Left   + (b.Left   - a.Left)   * t,
        a.Top    + (b.Top    - a.Top)     * t,
        a.Right  + (b.Right  - a.Right)  * t,
        a.Bottom + (b.Bottom - a.Bottom) * t
    );

    Crop IKeyframeInterpolatable<Crop>.KeyframeInterpolate(Crop other, double t) =>
        Interpolate(this, other, t);

    public Crop Clone() => new(Left, Top, Right, Bottom);

    public bool Equals(Crop? other) =>
        other is not null &&
        Left == other.Left && Top == other.Top &&
        Right == other.Right && Bottom == other.Bottom;

    public override bool Equals(object? obj) => Equals(obj as Crop);
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
}
