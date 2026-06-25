using System;
using System.Threading.Tasks;

namespace Palmier.Media;

public interface IFrameProvider
{
    Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height);
}

public class FrameProvider : IFrameProvider
{
    private readonly IFrameCache _cache;

    public FrameProvider(IFrameCache cache)
    {
        _cache = cache;
    }

    public async Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height)
    {
        string cacheKey = $"{assetPath}_{width}x{height}";
        var cached = _cache.GetFrame(cacheKey, sourceFrame);
        if (cached != null) return cached;

        int size = width * height * 4;
        byte[] pixelData = new byte[size];

        await Task.Run(() =>
        {
            int checkSize = 32;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    bool isWhite = ((x / checkSize) + (y / checkSize)) % 2 == 0;
                    byte color = (byte)(isWhite ? 200 : 50);
                    
                    pixelData[index] = color;     // B
                    pixelData[index + 1] = color; // G
                    pixelData[index + 2] = color; // R
                    pixelData[index + 3] = 255;   // A
                }
            }
        });

        _cache.AddFrame(cacheKey, sourceFrame, pixelData);
        return pixelData;
    }
}
