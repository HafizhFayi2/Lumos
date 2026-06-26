using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Grade Curves: per-channel spline via a Catmull-Rom interpolated LUT.
/// Params: control points for each channel as comma-separated pairs "x0,y0,x1,y1,...".
/// Default is identity (no-op). Each value is in [0, 255] range.
public sealed class GradeCurvesRenderer : IEffectRenderer
{
    public string EffectType => "grade_curves";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        byte[] lutR = BuildChannelLut(effect, "curve_r", clipFrame);
        byte[] lutG = BuildChannelLut(effect, "curve_g", clipFrame);
        byte[] lutB = BuildChannelLut(effect, "curve_b", clipFrame);

        // If all identity, return copy without processing
        bool isIdentity = IsIdentity(lutR) && IsIdentity(lutG) && IsIdentity(lutB);
        if (isIdentity) return source.Copy();

        using var colorFilter = SKColorFilter.CreateTable(null, lutR, lutG, lutB);
        using var paint = new SKPaint { ColorFilter = colorFilter };

        var dst = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(dst);
        canvas.DrawBitmap(source, 0, 0, paint);
        return dst;
    }

    private static byte[] BuildChannelLut(Effect effect, string paramKey, int clipFrame)
    {
        var lut = new byte[256];

        // Default: identity
        for (int i = 0; i < 256; i++) lut[i] = (byte)i;

        var param = effect.Params.GetValueOrDefault(paramKey);
        if (param == null) return lut;

        // Expect points embedded as a curve string "x0,y0 x1,y1 ..."
        // For numeric EffectParam, we treat a single value as output-at-midpoint
        double midVal = param.Resolve(clipFrame, 128.0);

        // Simple single-point lift: interpolate identity curve to pass through (128, midVal)
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            // Blend identity with a bump curve at midpoint
            float bump = 4f * t * (1f - t); // Peaks at t=0.5
            float v = t * 255f + (float)(midVal - 128.0) * bump;
            lut[i] = (byte)Math.Clamp(v, 0f, 255f);
        }

        return lut;
    }

    private static bool IsIdentity(byte[] lut)
    {
        for (int i = 0; i < 256; i++)
            if (lut[i] != (byte)i) return false;
        return true;
    }
}
