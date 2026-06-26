using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

/// Color Wheels: classic DaVinci-style Lift/Gamma/Gain per channel.
/// Lift shifts the shadows, Gamma adjusts midtones, Gain scales highlights.
public sealed class ColorWheelsRenderer : IEffectRenderer
{
    public string EffectType => "color_wheels";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        // Lift: [-1, 1] additive offset in shadow range
        float liftR = (float)(effect.Params.GetValueOrDefault("lift_r")?.Resolve(clipFrame, 0) ?? 0.0);
        float liftG = (float)(effect.Params.GetValueOrDefault("lift_g")?.Resolve(clipFrame, 0) ?? 0.0);
        float liftB = (float)(effect.Params.GetValueOrDefault("lift_b")?.Resolve(clipFrame, 0) ?? 0.0);

        // Gamma: [0.1, 9.99] — applied as pow(x, 1/gamma)
        float gammaR = (float)(effect.Params.GetValueOrDefault("gamma_r")?.Resolve(clipFrame, 1.0) ?? 1.0);
        float gammaG = (float)(effect.Params.GetValueOrDefault("gamma_g")?.Resolve(clipFrame, 1.0) ?? 1.0);
        float gammaB = (float)(effect.Params.GetValueOrDefault("gamma_b")?.Resolve(clipFrame, 1.0) ?? 1.0);

        // Gain: [0, 4] — multiplicative scale on highlights
        float gainR = (float)(effect.Params.GetValueOrDefault("gain_r")?.Resolve(clipFrame, 1.0) ?? 1.0);
        float gainG = (float)(effect.Params.GetValueOrDefault("gain_g")?.Resolve(clipFrame, 1.0) ?? 1.0);
        float gainB = (float)(effect.Params.GetValueOrDefault("gain_b")?.Resolve(clipFrame, 1.0) ?? 1.0);

        gammaR = Math.Clamp(gammaR, 0.1f, 9.99f);
        gammaG = Math.Clamp(gammaG, 0.1f, 9.99f);
        gammaB = Math.Clamp(gammaB, 0.1f, 9.99f);

        // Build per-channel LUTs
        byte[] lutR = BuildLut(liftR, gammaR, gainR);
        byte[] lutG = BuildLut(liftG, gammaG, gainG);
        byte[] lutB = BuildLut(liftB, gammaB, gainB);

        using var colorFilter = SKColorFilter.CreateTable(null, lutR, lutG, lutB);
        using var paint = new SKPaint { ColorFilter = colorFilter };

        var dst = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(dst);
        canvas.DrawBitmap(source, 0, 0, paint);
        return dst;
    }

    private static byte[] BuildLut(float lift, float gamma, float gain)
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float v = i / 255f;
            // Apply lift (shadow offset)
            v = v + lift * (1f - v);
            v = Math.Clamp(v, 0f, 1f);
            // Apply gamma (midtone power)
            v = MathF.Pow(v, 1f / gamma);
            // Apply gain (highlight scale)
            v = v * gain;
            lut[i] = (byte)Math.Clamp(v * 255f + 0.5f, 0f, 255f);
        }
        return lut;
    }
}
