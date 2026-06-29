using System.Buffers;

namespace Lumos.Media;

/// Shared software compositing helpers used by PlaybackEngine and VideoEngine.
/// Centralizes the alpha-blend and slot-compositing logic that was duplicated
/// across both engines. Uses pooled buffers to reduce GC pressure.
public static class MediaCompositor
{
    /// Composite a list of visual slots into a BGRA byte array.
    /// The returned array is from the shared ArrayPool — the caller must consume
    /// it before the next composite call or copy it out.
    public static async Task<byte[]> CompositeSlotsAsync(
        IReadOnlyList<CompositionSlot> slots,
        IFrameProvider frameProvider,
        int width,
        int height)
    {
        int byteCount = width * height * 4;
        var dest = FrameBufferPool.Rent(byteCount);
        try
        {
            // Zero out the buffer (pooled array may have stale data from previous use)
            Array.Clear(dest, 0, byteCount);

            foreach (var slot in slots)
            {
                if (slot.Opacity <= 0) continue;

                var src = await frameProvider.GetFrameAsync(slot.AssetPath, slot.SourceFrame, width, height);
                if (src == null || src.Length < 4) continue;

                Blend(src, dest, slot.Opacity);
            }

            // Copy exact byte count to new array (ArrayPool.Rent may return larger buffers)
            var result = new byte[byteCount];
            Buffer.BlockCopy(dest, 0, result, 0, byteCount);
            return result;
        }
        finally
        {
            FrameBufferPool.Return(dest);
        }
    }

    /// Alpha-blend source BGRA pixels onto destination BGRA pixels.
    private static void Blend(byte[] src, byte[] dest, double opacity)
    {
        int len = Math.Min(src.Length, dest.Length);

        if (opacity >= 1.0)
        {
            Buffer.BlockCopy(src, 0, dest, 0, len);
            return;
        }

        for (int i = 0; i < len; i += 4)
        {
            byte sb = src[i];
            byte sg = src[i + 1];
            byte sr = src[i + 2];
            byte sa = src[i + 3];

            double a = (sa / 255.0) * opacity;
            if (a <= 0) continue;

            dest[i]     = (byte)(sb * a + dest[i] * (1.0 - a));
            dest[i + 1] = (byte)(sg * a + dest[i + 1] * (1.0 - a));
            dest[i + 2] = (byte)(sr * a + dest[i + 2] * (1.0 - a));
            dest[i + 3] = (byte)Math.Max(dest[i + 3], (byte)(sa * opacity));
        }
    }
}
