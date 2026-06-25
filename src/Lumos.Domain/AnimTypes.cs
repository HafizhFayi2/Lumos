namespace Lumos.Domain;

/// Constraint for types that can be interpolated between keyframes.
public interface IKeyframeInterpolatable<T>
{
    T KeyframeInterpolate(T other, double t);
}

/// Double interpolation.
public readonly struct AnimDouble : IKeyframeInterpolatable<AnimDouble>, IEquatable<AnimDouble>
{
    public double Value { get; }
    public AnimDouble(double value) { Value = value; }
    public AnimDouble KeyframeInterpolate(AnimDouble other, double t) =>
        new(Value + (other.Value - Value) * t);
    public static implicit operator AnimDouble(double v) => new(v);
    public static implicit operator double(AnimDouble v) => v.Value;
    public bool Equals(AnimDouble other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is AnimDouble d && Equals(d);
    public override int GetHashCode() => Value.GetHashCode();
}

/// Two-component keyframe value for position (X,Y) and scale (W,H).
public sealed class AnimPair : IKeyframeInterpolatable<AnimPair>, IEquatable<AnimPair>
{
    public double A { get; set; }
    public double B { get; set; }

    public AnimPair() { }
    public AnimPair(double a, double b) { A = a; B = b; }

    public AnimPair KeyframeInterpolate(AnimPair other, double t) =>
        new(A + (other.A - A) * t, B + (other.B - B) * t);

    public AnimPair Clone() => new(A, B);
    public bool Equals(AnimPair? other) => other is not null && A == other.A && B == other.B;
    public override bool Equals(object? obj) => Equals(obj as AnimPair);
    public override int GetHashCode() => HashCode.Combine(A, B);
}
