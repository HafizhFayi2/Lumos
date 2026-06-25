using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using Lumos.Media;

namespace Lumos.Infrastructure;

/// CPU-based compositor using SkiaSharp.
/// Composites all visual slots in painter's order and outputs raw BGRA bytes.
public sealed class SkiaCompositor : IFrameCompositor, IDisposable
{
    private readonly SKSurface _surface;
    private readonly SKCanvas  _canvas;
    private readonly int _width;
    private readonly int _height;

    public SkiaCompositor(int width = 1920, int height = 1080)
    {
        _width  = width;
        _height = height;
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

            SKBitmap? bitmap = await LoadBitmapAsync(slot.AssetPath);
            if (bitmap == null) continue;

            using (bitmap)
            {
                var destRect = new SKRect(0, 0, _width, _height);
                using var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.Low,
                    Color = new SKColor(255, 255, 255, (byte)Math.Clamp(slot.Opacity * 255, 0, 255)),
                };
                _canvas.DrawBitmap(bitmap, destRect, paint);
            }
        }

        _canvas.Flush();
        using var image = _surface.Snapshot();
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        using var decoded = SKBitmap.Decode(data);
        if (decoded == null) return new byte[_width * _height * 4];
        return decoded.Bytes;
    }

    private static Task<SKBitmap?> LoadBitmapAsync(string path)
    {
        if (!File.Exists(path))
            return Task.FromResult<SKBitmap?>(null);
        try
        {
            return Task.FromResult<SKBitmap?>(SKBitmap.Decode(path));
        }
        catch
        {
            return Task.FromResult<SKBitmap?>(null);
        }
    }

    public void Dispose()
    {
        _surface.Dispose();
    }
}
