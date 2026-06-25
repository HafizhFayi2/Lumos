using System;
using System.IO;
using System.Threading.Tasks;
using Lumos.Application;
using Lumos.Domain;

namespace Lumos.Infrastructure;

/// Generates clip thumbnails and audio waveform data.
/// Uses a checkerboard placeholder until FFmpeg bindings are wired.
public sealed class ThumbnailGenerator : IThumbnailGenerator
{
    private readonly string _cacheDir;

    public ThumbnailGenerator(string cacheDir)
    {
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
    }

    public async Task<string> GenerateThumbnailAsync(Asset asset, TimeSpan position)
    {
        string key    = $"{Path.GetFileNameWithoutExtension(asset.FilePath)}_{(long)position.TotalMilliseconds}";
        string target = Path.Combine(_cacheDir, $"{key}.png");

        if (File.Exists(target)) return target;

        // Write a 160×90 BGRA raw placeholder (will be replaced by FFmpeg extraction)
        await Task.Run(() =>
        {
            const int W = 160, H = 90;
            byte[] data = GenerateCheckerboard(W, H);
            WritePngRaw(target, W, H, data);
        });

        return target;
    }

    public async Task<List<string>> GenerateWaveformAsync(Asset asset)
    {
        // Return empty waveform segments until PCM extraction is wired
        return await Task.FromResult(new List<string>());
    }

    // Minimal PNG writer (BGRA → RGBA, no compression — dev preview only)
    private static void WritePngRaw(string path, int width, int height, byte[] bgra)
    {
        // Convert BGRA → RGBA
        byte[] rgba = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            rgba[i]     = bgra[i + 2]; // R
            rgba[i + 1] = bgra[i + 1]; // G
            rgba[i + 2] = bgra[i];     // B
            rgba[i + 3] = bgra[i + 3]; // A
        }

        using var ms = new MemoryStream();
        WritePngToStream(ms, width, height, rgba);
        File.WriteAllBytes(path, ms.ToArray());
    }

    private static void WritePngToStream(Stream s, int w, int h, byte[] rgba)
    {
        // PNG signature
        byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
        s.Write(sig, 0, sig.Length);

        // IHDR
        WriteChunk(s, "IHDR", ihdr =>
        {
            WriteInt32BE(ihdr, w);
            WriteInt32BE(ihdr, h);
            ihdr.WriteByte(8);  // bit depth
            ihdr.WriteByte(2);  // color type: RGB (we'll write RGBA as RGB+alpha but use type 2 for simplicity)
        });

        // IDAT — uncompressed raw using zlib stored blocks
        WriteChunk(s, "IDAT", idat =>
        {
            // Build raw image data (filter byte 0 per row)
            int rowBytes = w * 3;
            byte[] raw = new byte[h * (1 + rowBytes)];
            for (int y = 0; y < h; y++)
            {
                int dstBase = y * (1 + rowBytes);
                raw[dstBase] = 0; // filter none
                for (int x = 0; x < w; x++)
                {
                    int src = (y * w + x) * 4;
                    int dst = dstBase + 1 + x * 3;
                    raw[dst]     = rgba[src];
                    raw[dst + 1] = rgba[src + 1];
                    raw[dst + 2] = rgba[src + 2];
                }
            }
            WriteZlibStored(idat, raw);
        });

        // IEND
        WriteChunk(s, "IEND", _ => { });
    }

    private static void WriteChunk(Stream s, string type, Action<MemoryStream> write)
    {
        var data = new MemoryStream();
        write(data);
        byte[] payload = data.ToArray();
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        WriteInt32BE(s, payload.Length);
        s.Write(typeBytes, 0, 4);
        s.Write(payload, 0, payload.Length);

        uint crc = Crc32(typeBytes, payload);
        WriteInt32BE(s, (int)crc);
    }

    private static void WriteZlibStored(Stream s, byte[] data)
    {
        s.WriteByte(0x78); // CMF
        s.WriteByte(0x01); // FLG (no dict, level 0)

        int offset = 0;
        int remaining = data.Length;
        while (remaining > 0)
        {
            int blockSize = Math.Min(remaining, 65535);
            bool isFinal  = (remaining - blockSize) == 0;
            s.WriteByte((byte)(isFinal ? 1 : 0));
            s.WriteByte((byte)(blockSize & 0xFF));
            s.WriteByte((byte)(blockSize >> 8));
            s.WriteByte((byte)(~blockSize & 0xFF));
            s.WriteByte((byte)((~blockSize >> 8) & 0xFF));
            s.Write(data, offset, blockSize);
            offset    += blockSize;
            remaining -= blockSize;
        }

        // Adler-32
        uint a = 1, b = 0;
        foreach (byte byt in data) { a = (a + byt) % 65521; b = (b + a) % 65521; }
        uint adler = (b << 16) | a;
        WriteInt32BE(s, (int)adler);
    }

    private static void WriteInt32BE(Stream s, int v)
    {
        s.WriteByte((byte)(v >> 24));
        s.WriteByte((byte)(v >> 16));
        s.WriteByte((byte)(v >> 8));
        s.WriteByte((byte)v);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type)  crc = Crc32Step(crc, b);
        foreach (byte b in data)  crc = Crc32Step(crc, b);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint Crc32Step(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        return crc;
    }

    private static byte[] GenerateCheckerboard(int w, int h)
    {
        const int cell = 16;
        byte[] data = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w + x) * 4;
                bool light = ((x / cell) + (y / cell)) % 2 == 0;
                byte v = (byte)(light ? 180 : 80);
                data[idx]     = v;
                data[idx + 1] = v;
                data[idx + 2] = v;
                data[idx + 3] = 255;
            }
        return data;
    }
}
