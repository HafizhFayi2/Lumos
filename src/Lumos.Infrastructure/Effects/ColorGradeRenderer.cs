using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Color grade: saturation boost + contrast + warmth via SKColorFilter matrix.
public sealed class ColorGradeRenderer : IEffectRenderer
{
    public string EffectType => "color_grade";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float temperature = (float)(effect.Params.GetValueOrDefault("temperature")?.Resolve(clipFrame, 0) ?? 0.0);
        float tint        = (float)(effect.Params.GetValueOrDefault("tint")?.Resolve(clipFrame, 0) ?? 0.0);
        float saturation  = (float)(effect.Params.GetValueOrDefault("saturation")?.Resolve(clipFrame, 1) ?? 1.0);
        float contrast    = (float)(effect.Params.GetValueOrDefault("contrast")?.Resolve(clipFrame, 1.05) ?? 1.05f);
        float warmth      = (float)(effect.Params.GetValueOrDefault("warmth")?.Resolve(clipFrame, 0.04) ?? 0.04f);

        // Build a 4x5 colour matrix (row-major: [R,G,B,A,bias])
        // Saturation via luminance blend, contrast via scale+offset, warmth via R channel boost
        float lumR = 0.2126f, lumG = 0.7152f, lumB = 0.0722f;
        float sr = (1 - saturation) * lumR;
        float sg = (1 - saturation) * lumG;
        float sb = (1 - saturation) * lumB;

        float bias = (1 - contrast) * 0.5f;

        float[] matrix =
        {
            contrast * (saturation + sr + warmth), contrast * sg,              contrast * sb,              0, bias,
            contrast * sr,                          contrast * (saturation + sg), contrast * sb,              0, bias,
            contrast * sr,                          contrast * sg,              contrast * (saturation + sb), 0, bias,
            0,                                      0,                          0,                          1, 0
        };

        using var colorFilter = SKColorFilter.CreateColorMatrix(matrix);
        using var paint = new SKPaint { ColorFilter = colorFilter };

        var dst = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(dst);
        canvas.DrawBitmap(source, 0, 0, paint);
        return dst;
    }
}
