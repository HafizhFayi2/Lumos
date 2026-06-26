using Lumos.Domain;
using SkiaSharp;

namespace Lumos.Infrastructure.Effects;

public sealed class GrainRenderer : IEffectRenderer
{
    public string EffectType => "grain";
    private readonly Random _rand = new(1337);

    public SKBitmap Apply(SKBitmap source, Effect effect, int clipFrame)
    {
        float amount = (float)(effect.Params.GetValueOrDefault("amount")?.Resolve(clipFrame, 0.2) ?? 0.2);
        if (amount == 0) return source.Copy();

        // Create a noise bitmap
        int cw = source.Width / 4; 
        int ch = source.Height / 4;
        using var noise = new SKBitmap(cw, ch, SKColorType.Gray8, SKAlphaType.Opaque);
        
        unsafe
        {
            byte* ptr = (byte*)noise.GetPixels();
            for (int i = 0; i < cw * ch; i++)
            {
                ptr[i] = (byte)_rand.Next(0, 256);
            }
        }

        var dst = source.Copy();
        using var canvas = new SKCanvas(dst);
        using var paint = new SKPaint
        {
            BlendMode = SKBlendMode.Overlay,
            Color = new SKColor(255, 255, 255, (byte)(amount * 255)),
            FilterQuality = SKFilterQuality.Low
        };
        
        var rect = new SKRect(0, 0, dst.Width, dst.Height);
        canvas.DrawBitmap(noise, rect, paint);
        
        return dst;
    }
}
