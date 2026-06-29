using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Lumos.Media;

/// <summary>
/// Pooled frame buffer management to reduce GC pressure from repeated
/// large byte[] allocations (e.g. 1920×1080×4 ≈ 8 MB per frame).
/// Wraps <see cref="ArrayPool{T}"/> with frame-specific helpers.
/// </summary>
public static class FrameBufferPool
{
    /// <summary>
    /// The shared <see cref="ArrayPool{T}"/> instance used for all frame buffers.
    /// </summary>
    public static ArrayPool<byte> Shared => ArrayPool<byte>.Shared;

    /// <summary>
    /// Rent a byte buffer large enough for a BGRA frame of the given dimensions.
    /// The returned array may be larger than requested — use <paramref name="minLength"/>
    /// to track the actual usable size.
    /// </summary>
    public static byte[] Rent(int minLength) => Shared.Rent(minLength);

    /// <summary>
    /// Return a buffer to the pool.
    /// </summary>
    public static void Return(byte[] buffer, bool clearArray = false) =>
        Shared.Return(buffer, clearArray);

    /// <summary>
    /// Convenience: rent a buffer sized for a BGRA frame at the given resolution.
    /// </summary>
    public static byte[] RentFrame(int width, int height) =>
        Rent(width * height * 4);

    /// <summary>
    /// Fill a BGRA buffer with opaque black pixels (R=0, G=0, B=0, A=255).
    /// Only sets the alpha channel; RGB assumed zero from previous zero-init or overwrite.
    /// </summary>
    public static void FillBlack(Span<byte> buffer)
    {
        for (int i = 3; i < buffer.Length; i += 4)
            buffer[i] = 255;
    }

    /// <summary>
    /// Fill a BGRA buffer with a solid color.
    /// </summary>
    public static void FillColor(Span<byte> buffer, byte r, byte g, byte b, byte a = 255)
    {
        for (int i = 0; i < buffer.Length; i += 4)
        {
            buffer[i]     = b;
            buffer[i + 1] = g;
            buffer[i + 2] = r;
            buffer[i + 3] = a;
        }
    }

    /// <summary>
    /// Get the byte count needed for a BGRA frame at the given resolution.
    /// </summary>
    public static int FrameByteCount(int width, int height) => width * height * 4;
}
