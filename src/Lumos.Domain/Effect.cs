namespace Lumos.Domain;

/// One entry in a clip's ordered effect stack.
public sealed class Effect
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, EffectParam> Params { get; set; } = new();

    public static Effect Make(string type, Dictionary<string, double>? values = null)
    {
        var effect = new Effect { Type = type };
        if (values is not null)
            foreach (var (k, v) in values)
                effect.Params[k] = new EffectParam { NumericValue = v };
        return effect;
    }

    public Effect Clone()
    {
        var e = (Effect)MemberwiseClone();
        e.Id = Guid.NewGuid().ToString();
        e.Params = Params.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
        return e;
    }
}

/// A single effect parameter, which can be static or keyframe-animated.
public sealed class EffectParam
{
    public double? NumericValue { get; set; }
    public string? StringValue { get; set; }
    public KeyframeTrack<AnimDouble>? Track { get; set; }

    /// Effective numeric value at a clip-relative frame offset.
    public double Resolve(int offset, double defaultValue = 0)
    {
        if (Track is { IsActive: true })
            return Track.Sample(offset, new AnimDouble(NumericValue ?? defaultValue));
        return NumericValue ?? defaultValue;
    }

    public EffectParam Clone()
    {
        var p = (EffectParam)MemberwiseClone();
        p.Track = Track?.Clone();
        return p;
    }
}
