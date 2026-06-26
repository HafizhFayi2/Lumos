using Lumos.Domain;
using SkiaSharp;
using System;

namespace Lumos.Infrastructure.Effects;

public sealed class VignetteRenderer : IEffectRenderer
{
    public string EffectType => "vignette";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float intensity = (float)(effect.Params.GetValueOrDefault("intensity")?.Resolve(clipFrame, 0.5) ?? 0.5);
        float radius    = (float)(effect.Params.GetValueOrDefault("radius")?.Resolve(clipFrame, 0.8) ?? 0.8);
        
        var dst = source.Copy();
        using var canvas = new SKCanvas(dst);
        
        var colors = new[] { SKColors.Transparent, new SKColor(0, 0, 0, (byte)(intensity * 255)) };
        var pos = new[] { radius, 1.0f };
        
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(dst.Width / 2f, dst.Height / 2f),
            Math.Max(dst.Width, dst.Height) / 2f,
            colors,
            pos,
            SKShaderTileMode.Clamp);
            
        using var paint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.Multiply };
        canvas.DrawRect(0, 0, dst.Width, dst.Height, paint);
        return dst;
    }
}
