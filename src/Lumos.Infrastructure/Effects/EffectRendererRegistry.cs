using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Registry of all available effect renderers. Renderers are registered once and looked up by effect type.
public sealed class EffectRendererRegistry
{
    private readonly Dictionary<string, IEffectRenderer> _renderers;

    public static readonly EffectRendererRegistry Default = new(new IEffectRenderer[]
    {
        new ColorGradeRenderer(),
        new ChromaKeyRenderer(),
        new LutRenderer(),
        new ClarityRenderer(),
        new GlowRenderer(),
        new GrainRenderer(),
        new VignetteRenderer(),
        new GradeCurvesRenderer(),
        new HighlightsShadowsRenderer(),
        new LUTTetraRenderer(),
        new LevelsRenderer(),
        new ColorWheelsRenderer(),
    });

    public EffectRendererRegistry(IEnumerable<IEffectRenderer> renderers)
    {
        _renderers = renderers.ToDictionary(r => r.EffectType, StringComparer.OrdinalIgnoreCase);
    }

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        if (!effect.Enabled) return source;
        if (_renderers.TryGetValue(effect.Type, out var renderer))
            return renderer.Apply(source, effect, clipFrame);
        return source;
    }

    public SKBitmap ApplyStack(SKBitmap source, IEnumerable<Effect> effects, int clipFrame)
    {
        var current = source;
        foreach (var effect in effects)
        {
            var next = Apply(current, effect, clipFrame);
            if (!ReferenceEquals(next, current))
                current.Dispose();
            current = next;
        }
        return current;
    }
}
