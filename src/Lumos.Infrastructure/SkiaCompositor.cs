using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;
using Lumos.Media;
using Lumos.Infrastructure.Effects;
using Lumos.Domain;
using System.Buffers;

namespace Lumos.Infrastructure;

/// CPU-based compositor using SkiaSharp.
/// Composites all visual slots in painter's order and outputs raw BGRA bytes.
/// Uses pooled buffers to reduce per-frame GC pressure on the hot path.
public sealed class SkiaCompositor : IFrameCompositor, IDisposable
{
    private readonly SKSurface _surface;
    private readonly SKCanvas  _canvas;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private readonly IFrameProvider? _frameProvider;
    // Reusable pixel buffer to avoid per-frame allocation of the composited output.
    // Only valid between Clear and Flush. Content must be consumed before the next
    // CompositeAsync call because pooled arrays are recycled.
    private byte[]? _outputBuffer;

    public SkiaCompositor(int width = 1920, int height = 1080, IFrameProvider? frameProvider = null)
    {
        _width  = width;
        _height = height;
        _stride = width * 4;
        _frameProvider = frameProvider;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info);
        _canvas  = _surface.Canvas;
    }

    /// <summary>
    /// Composite all visual slots and return raw BGRA pixel bytes.
    /// Returns a pooled array — the caller must consume it before the next
    /// call to CompositeAsync or use <see cref="CopyTo"/> to copy out.
    /// </summary>
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

        // Read pixels directly from the GPU surface instead of encoding to PNG
        // (which allocates an oversized memory stream + decode bitmap).
        // This eliminates the per-frame PNG encode/decode roundtrip.
        using var image = _surface.Snapshot();
        int byteCount = _width * _height * 4;

        // Return the previous frame's buffer to the pool before renting a new one.
        if (_outputBuffer != null)
        {
            FrameBufferPool.Return(_outputBuffer);
            _outputBuffer = null;
        }

        var pixels = FrameBufferPool.Rent(byteCount);

        // Pin the pooled array so Skia can write pixels directly into it.
        var imgInfo = new SKImageInfo(_width, _height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            image.ReadPixels(imgInfo, handle.AddrOfPinnedObject(), _stride, 0, 0);
        }
        finally
        {
            handle.Free();
        }

        // Copy exact byte count to new array so callers always get the expected
        // array length (ArrayPool.Rent may return larger buffers).
        _outputBuffer = pixels;
        var result = new byte[byteCount];
        Buffer.BlockCopy(pixels, 0, result, 0, byteCount);
        return result;
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
        if (_outputBuffer != null)
        {
            FrameBufferPool.Return(_outputBuffer);
            _outputBuffer = null;
        }
        _surface.Dispose();
    }
}
