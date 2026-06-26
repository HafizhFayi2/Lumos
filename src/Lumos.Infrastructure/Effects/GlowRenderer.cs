using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

public sealed class GlowRenderer : IEffectRenderer
{
    public string EffectType => "glow";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float intensity = (float)(effect.Params.GetValueOrDefault("intensity")?.Resolve(clipFrame, 0.5) ?? 0.5);
        float radius = (float)(effect.Params.GetValueOrDefault("radius")?.Resolve(clipFrame, 10) ?? 10.0);

        var dst = source.Copy();
        using var canvas = new SKCanvas(dst);
        
        using var paint = new SKPaint 
        { 
            ImageFilter = SKImageFilter.CreateBlur(radius, radius),
            BlendMode = SKBlendMode.Screen,
            Color = new SKColor(255, 255, 255, (byte)(intensity * 255))
        };
        canvas.DrawBitmap(source, 0, 0, paint);
        return dst;
    }
}
