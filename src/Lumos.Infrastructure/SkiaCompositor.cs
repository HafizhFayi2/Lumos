using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;
using Lumos.Media;
using Lumos.Infrastructure.Effects;
using Lumos.Domain;

namespace Lumos.Infrastructure;

/// CPU-based compositor using SkiaSharp.
/// Composites all visual slots in painter's order and outputs raw BGRA bytes.
public sealed class SkiaCompositor : IFrameCompositor, IDisposable
{
    private readonly SKSurface _surface;
    private readonly SKCanvas  _canvas;
    private readonly int _width;
    private readonly int _height;
    private readonly IFrameProvider? _frameProvider;

    public SkiaCompositor(int width = 1920, int height = 1080, IFrameProvider? frameProvider = null)
    {
        _width  = width;
        _height = height;
        _frameProvider = frameProvider;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info);
        _canvas  = _surface.Canvas;
    }

    public async Task<byte[]> CompositeAsync(CompositionFrame frame)
    {
        _canvas.Clear(SKColors.Black);

        foreach (var slot in frame.Visual)
        {
            if (slot.Opacity <= 0) continue;

            SKBitmap? bitmap = await LoadBitmapAsync(slot);
            if (bitmap == null) continue;

            using (bitmap)
            {
                var processed = EffectRendererRegistry.Default.ApplyStack(bitmap, slot.Effects, frame.TimelineFrame);
                var destRect = new SKRect(0, 0, _width, _height);
                using var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.Low,
                    Color = new SKColor(255, 255, 255, (byte)Math.Clamp(slot.Opacity * 255, 0, 255)),
                };
                _canvas.DrawBitmap(processed, destRect, paint);
                if (!ReferenceEquals(processed, bitmap)) processed.Dispose();
            }
        }

        _canvas.Flush();
        using var image = _surface.Snapshot();
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        using var decoded = SKBitmap.Decode(data);
        if (decoded == null) return new byte[_width * _height * 4];
        return decoded.Bytes;
    }

    private async Task<SKBitmap?> LoadBitmapAsync(CompositionSlot slot)
    {
        if (slot.RenderType == ClipType.Video && _frameProvider != null)
        {
            var pixels = await _frameProvider.GetFrameAsync(slot.AssetPath, slot.SourceFrame, _width, _height);
            if (pixels == null) return null;

            var bitmap = new SKBitmap(new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul));
            Marshal.Copy(pixels, 0, bitmap.GetPixels(), Math.Min(pixels.Length, bitmap.ByteCount));
            return bitmap;
        }

        if (!File.Exists(slot.AssetPath))
            return null;
        try
        {
            return SKBitmap.Decode(slot.AssetPath);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _surface.Dispose();
    }
}
