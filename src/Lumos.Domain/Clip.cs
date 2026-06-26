namespace Lumos.Domain;

public sealed class Clip
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MediaRef { get; set; } = string.Empty;
    public ClipType MediaType { get; set; } = ClipType.Video;
    public ClipType SourceClipType { get; set; } = ClipType.Video;

    // Timeline placement (integer frames — project fps units)
    public int StartFrame { get; set; }
    public int DurationFrames { get; set; }
    public int TrimStartFrame { get; set; }
    public int TrimEndFrame { get; set; }

    public double Speed { get; set; } = 1.0;
    public double Volume { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;

    public int FadeInFrames { get; set; }
    public int FadeOutFrames { get; set; }
    public Interpolation FadeInInterpolation { get; set; } = Interpolation.Linear;
    public Interpolation FadeOutInterpolation { get; set; } = Interpolation.Linear;

    public Transform Transform { get; set; } = new();
    public Crop Crop { get; set; } = new();
    
    public List<Effect> Effects { get; set; } = new();

    public string? LinkGroupId { get; set; }
    public string? CaptionGroupId { get; set; }

    // Text clips only
    public string? TextContent { get; set; }

    // Keyframe tracks — null when no animation on that property
    public KeyframeTrack<AnimDouble>? OpacityTrack { get; set; }
    public KeyframeTrack<AnimPair>?   PositionTrack { get; set; }
    public KeyframeTrack<AnimPair>?   ScaleTrack { get; set; }
    public KeyframeTrack<AnimDouble>? RotationTrack { get; set; }
    public KeyframeTrack<Crop>?       CropTrack { get; set; }
    public KeyframeTrack<AnimDouble>? VolumeTrack { get; set; }

    // Computed
    public int EndFrame => StartFrame + DurationFrames;
    public int SourceFramesConsumed => (int)Math.Round(DurationFrames * Speed);
    public int SourceDurationFrames => SourceFramesConsumed + TrimStartFrame + TrimEndFrame;

    private int KeyframeOffset(int timelineFrame) => timelineFrame - StartFrame;

    public bool Contains(int timelineFrame) =>
        timelineFrame >= StartFrame && timelineFrame < EndFrame;

    // ── Sampling ──────────────────────────────────────────────────────────────

    public double OpacityAt(int frame)
    {
        double raw = RawOpacityAt(frame);
        if (MediaType == ClipType.Audio) return raw;
        if (FadeInFrames == 0 && FadeOutFrames == 0) return raw;
        return raw * FadeMultiplier(frame);
    }

    public double RawOpacityAt(int frame) =>
        OpacityTrack?.Sample(KeyframeOffset(frame), new AnimDouble(Opacity)) ?? Opacity;

    public double RotationAt(int frame) =>
        RotationTrack?.Sample(KeyframeOffset(frame), new AnimDouble(Transform.Rotation)) ?? Transform.Rotation;

    public (double X, double Y) TopLeftAt(int frame)
    {
        if (PositionTrack is { IsActive: true })
        {
            var p = PositionTrack.Sample(KeyframeOffset(frame), new AnimPair(0, 0));
            return (p.A, p.B);
        }
        var c = Transform.Center;
        var sz = SizeAt(frame);
        return (c.X - sz.Width / 2, c.Y - sz.Height / 2);
    }

    public (double Width, double Height) SizeAt(int frame)
    {
        var fallback = new AnimPair(Transform.Width, Transform.Height);
        var s = ScaleTrack?.Sample(KeyframeOffset(frame), fallback) ?? fallback;
        return (s.A, s.B);
    }

    public Transform TransformAt(int frame)
    {
        var tl = TopLeftAt(frame);
        var sz = SizeAt(frame);
        var t = Transform.FromTopLeft(tl.X, tl.Y, sz.Width, sz.Height);
        t.Rotation = RotationAt(frame);
        return t;
    }

    public Crop CropAt(int frame) =>
        CropTrack?.Sample(KeyframeOffset(frame), Crop) ?? Crop;

    public double VolumeAt(int frame)
    {
        double kfGain = 1.0;
        if (VolumeTrack is { IsActive: true })
            kfGain = DbToLinear((double)(VolumeTrack.Sample(frame - StartFrame, new AnimDouble(0))));
        return Volume * kfGain * FadeMultiplier(frame);
    }

    /// 0-1 envelope from fade head/tail ramps.
    public double FadeMultiplier(int frame)
    {
        int rel = frame - StartFrame;
        if (rel < 0 || rel > DurationFrames) return 0;
        double inMul = FadeInFrames > 0
            ? Ease(Math.Min(1.0, (double)rel / FadeInFrames), FadeInInterpolation)
            : 1.0;
        int outRem = DurationFrames - rel;
        double outMul = FadeOutFrames > 0
            ? Ease(Math.Min(1.0, (double)outRem / FadeOutFrames), FadeOutInterpolation)
            : 1.0;
        return Math.Min(inMul, outMul);
    }

    private static double Ease(double t, Interpolation interp) =>
        interp == Interpolation.Smooth ? t * t * (3 - 2 * t) : t;

    public bool HasTransformAnimation =>
        (PositionTrack?.IsActive ?? false) ||
        (ScaleTrack?.IsActive ?? false) ||
        (RotationTrack?.IsActive ?? false);

    // ── Mutation helpers ──────────────────────────────────────────────────────

    public void SetDuration(int newDuration)
    {
        DurationFrames = newDuration;
        ClampKeyframesToDuration();
        ClampFadesToDuration();
    }

    public void ClampFadesToDuration()
    {
        FadeInFrames  = Math.Max(0, Math.Min(FadeInFrames, DurationFrames));
        FadeOutFrames = Math.Max(0, Math.Min(FadeOutFrames, DurationFrames - FadeInFrames));
    }

    public void SetFade(FadeEdge edge, int frames)
    {
        int v = Math.Max(0, frames);
        if (edge == FadeEdge.Left) FadeInFrames  = v;
        else                       FadeOutFrames = v;
        ClampFadesToDuration();
    }

    public void ClampKeyframesToDuration()
    {
        ClampTrack(OpacityTrack);
        ClampTrack(PositionTrack);
        ClampTrack(ScaleTrack);
        ClampTrack(RotationTrack);
        ClampTrack(CropTrack);
        ClampTrack(VolumeTrack);

        void ClampTrack<T>(KeyframeTrack<T>? track) where T : notnull, IKeyframeInterpolatable<T>
        {
            if (track is null) return;
            foreach (var kf in track.Keyframes.Where(k => k.Frame > DurationFrames).ToList())
                track.Remove(kf.Frame);
        }
    }

    public Clip Clone(bool newId = false)
    {
        var c = (Clip)MemberwiseClone();
        if (newId) c.Id = Guid.NewGuid().ToString();
        c.Transform = Transform.Clone();
        c.Crop = Crop.Clone();
        c.OpacityTrack  = OpacityTrack?.Clone();
        c.PositionTrack = PositionTrack?.Clone();
        c.ScaleTrack    = ScaleTrack?.Clone();
        c.RotationTrack = RotationTrack?.Clone();
        c.CropTrack     = CropTrack?.Clone();
        c.VolumeTrack   = VolumeTrack?.Clone();
        c.Effects       = Effects.Select(e => e.Clone()).ToList();
        return c;
    }

    private static double DbToLinear(double db) => Math.Pow(10, db / 20.0);
}
