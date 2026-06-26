using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

public sealed class ClarityRenderer : IEffectRenderer
{
    public string EffectType => "clarity";

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float amount = (float)(effect.Params.GetValueOrDefault("amount")?.Resolve(clipFrame, 0) ?? 0.5);
        if (amount == 0) return source.Copy();

        // Simulate clarity by doing unsharp mask on midtones.
        // For now, using a simple blur and blend (high pass filter).
        using var blurred = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(blurred);
        using var paint = new SKPaint { ImageFilter = SKImageFilter.CreateBlur(10, 10) };
        canvas.DrawBitmap(source, 0, 0, paint);

        // Actual clarity requires pixel manipulation (original + (original - blurred) * amount)
        // Since Skia's built-in blend modes don't easily do (O - B), we do a simple overlay approximation here.
        var dst = source.Copy();
        using var dstCanvas = new SKCanvas(dst);
        using var overlayPaint = new SKPaint
        {
            BlendMode = SKBlendMode.Overlay,
            Color = new SKColor(255, 255, 255, (byte)(Math.Clamp(amount, 0, 1) * 128))
        };
        dstCanvas.DrawBitmap(blurred, 0, 0, overlayPaint);
        return dst;
    }
}
