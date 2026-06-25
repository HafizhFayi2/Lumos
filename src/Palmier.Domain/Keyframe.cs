namespace Palmier.Domain;

public sealed class Keyframe<T> where T : notnull
{
    public int Frame { get; set; }
    public T Value { get; set; }
    public Interpolation InterpolationOut { get; set; } = Interpolation.Smooth;

    public Keyframe(int frame, T value, Interpolation interpolation = Interpolation.Smooth)
    {
        Frame = frame; Value = value; InterpolationOut = interpolation;
    }
}

public sealed class KeyframeTrack<T> where T : notnull, IKeyframeInterpolatable<T>
{
    private readonly List<Keyframe<T>> _keyframes = new();

    public IReadOnlyList<Keyframe<T>> Keyframes => _keyframes;
    public bool IsActive => _keyframes.Count > 0;

    public void Upsert(Keyframe<T> kf)
    {
        var idx = _keyframes.FindIndex(k => k.Frame == kf.Frame);
        if (idx >= 0)
            _keyframes[idx] = kf;
        else
        {
            var at = _keyframes.FindIndex(k => k.Frame > kf.Frame);
            if (at < 0) _keyframes.Add(kf);
            else _keyframes.Insert(at, kf);
        }
    }

    public void Remove(int frame) => _keyframes.RemoveAll(k => k.Frame == frame);

    public void Move(int oldFrame, int newFrame)
    {
        var idx = _keyframes.FindIndex(k => k.Frame == oldFrame);
        if (idx < 0) return;
        if (newFrame != oldFrame && _keyframes.Any(k => k.Frame == newFrame)) return;
        var kf = _keyframes[idx];
        _keyframes.RemoveAt(idx);
        kf.Frame = newFrame;
        Upsert(kf);
    }

    public void Clear() => _keyframes.Clear();

    /// Sample the track at the given clip-relative frame offset.
    public T Sample(int frame, T fallback)
    {
        if (_keyframes.Count == 0) return fallback;
        if (_keyframes.Count == 1) return _keyframes[0].Value;
        if (frame <= _keyframes[0].Frame) return _keyframes[0].Value;
        if (frame >= _keyframes[^1].Frame) return _keyframes[^1].Value;

        int bIdx = _keyframes.FindIndex(k => k.Frame > frame);
        if (bIdx < 0) return _keyframes[^1].Value;

        var a = _keyframes[bIdx - 1];
        var b = _keyframes[bIdx];
        double raw = (double)(frame - a.Frame) / (b.Frame - a.Frame);
        double t = a.InterpolationOut switch
        {
            Interpolation.Hold   => 0,
            Interpolation.Linear => raw,
            Interpolation.Smooth => Smoothstep(raw),
            _ => raw
        };
        return a.InterpolationOut == Interpolation.Hold
            ? a.Value
            : a.Value.KeyframeInterpolate(b.Value, t);
    }

    public KeyframeTrack<T> Clone()
    {
        var copy = new KeyframeTrack<T>();
        foreach (var kf in _keyframes)
            copy._keyframes.Add(new Keyframe<T>(kf.Frame, kf.Value, kf.InterpolationOut));
        return copy;
    }

    private static double Smoothstep(double t) => t * t * (3 - 2 * t);
}
