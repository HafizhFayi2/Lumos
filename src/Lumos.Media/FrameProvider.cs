using System;
using System.Threading.Tasks;

namespace Lumos.Media;

public interface IFrameProvider
{
    Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height);
}

/// IFrameProvider implementation backed by DecodePipeline (FFmpeg via ffmpeg.exe on PATH).
/// Falls back to a checkerboard placeholder when the asset cannot be decoded.
public sealed class FrameProvider : IFrameProvider, IDisposable
{
    private readonly IFrameCache _cache;
    private readonly DecodePipeline _pipeline;

    public FrameProvider(IFrameCache cache)
    {
        _cache    = cache;
        _pipeline = new DecodePipeline();
    }

    public async Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height)
    {
        string cacheKey = $"{assetPath}_{sourceFrame}_{width}x{height}";
        var cached = _cache.GetFrame(cacheKey, sourceFrame);
        if (cached != null) return cached;

        byte[] pixelData;
        try
        {
            pixelData = await _pipeline.DecodeFrameAsync(assetPath, sourceFrame, width, height);
        }
        catch
        {
            // Asset not decodable yet  return silent black frame
            pixelData = new byte[width * height * 4];
            FillBlack(pixelData, width, height);
        }

        _cache.AddFrame(cacheKey, sourceFrame, pixelData);
        return pixelData;
    }

    private static void FillBlack(byte[] buffer, int width, int height)
    {
        for (int i = 3; i < buffer.Length; i += 4)
            buffer[i] = 255; // alpha = 255, RGB stays 0 (black)
    }

    public void Dispose() => _pipeline.Dispose();
}
