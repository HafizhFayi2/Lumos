using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Levels: black point, white point, gamma — identical to Photoshop Levels.
public sealed class LevelsRenderer : IEffectRenderer
{
    public string EffectType => "levels";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float inBlack  = (float)(effect.Params.GetValueOrDefault("in_black")?.Resolve(clipFrame, 0)     ?? 0.0);
        float inWhite  = (float)(effect.Params.GetValueOrDefault("in_white")?.Resolve(clipFrame, 255)   ?? 255.0);
        float gamma    = (float)(effect.Params.GetValueOrDefault("gamma")?.Resolve(clipFrame, 1.0)      ?? 1.0);
        float outBlack = (float)(effect.Params.GetValueOrDefault("out_black")?.Resolve(clipFrame, 0)    ?? 0.0);
        float outWhite = (float)(effect.Params.GetValueOrDefault("out_white")?.Resolve(clipFrame, 255)  ?? 255.0);

        inBlack  = Math.Clamp(inBlack,  0, 254);
        inWhite  = Math.Clamp(inWhite,  inBlack + 1, 255);
        gamma    = Math.Clamp(gamma,    0.1f, 9.99f);
        outBlack = Math.Clamp(outBlack, 0, 254);
        outWhite = Math.Clamp(outWhite, outBlack + 1, 255);

        float inRange  = inWhite - inBlack;
        float outRange = outWhite - outBlack;

        byte[] lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float v = (i - inBlack) / inRange;
            v = Math.Clamp(v, 0f, 1f);
            if (gamma != 1f) v = MathF.Pow(v, 1f / gamma);
            lut[i] = (byte)Math.Clamp(outBlack + v * outRange, 0f, 255f);
        }

        // Apply via SKColorFilter table (one LUT applied uniformly to R, G, B)
        using var colorFilter = SKColorFilter.CreateTable(null, lut, lut, lut);
        using var paint = new SKPaint { ColorFilter = colorFilter };

        var dst = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(dst);
        canvas.DrawBitmap(source, 0, 0, paint);
        return dst;
    }
}
