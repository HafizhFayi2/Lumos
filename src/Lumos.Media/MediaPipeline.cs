using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Media;

public sealed class MediaPipeline : IDisposable
{
    private readonly DecodePipeline _decoder = new();
    private readonly IFrameCache _cache;
    private readonly ProxyManager _proxyManager;
    private bool _useGpuAcceleration;

    public ProxyManager ProxyManager => _proxyManager;
    public bool UseGpuAcceleration
    {
        get => _useGpuAcceleration;
        set => _useGpuAcceleration = value;
    }

    public MediaPipeline(IFrameCache cache, ProxyManager proxyManager)
    {
        _cache = cache;
        _proxyManager = proxyManager;
    }

    public async Task<byte[]?> GetFrameAsync(
        string assetPath,
        int frameIndex,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        string resolvedPath = _proxyManager.ResolvePath(assetPath);
        bool isProxy = resolvedPath != assetPath;

        int targetW = isProxy ? Math.Min(width, 1920) : width;
        int targetH = isProxy ? Math.Min(height, 1080) : height;

        string cacheKey = $"{resolvedPath}_{targetW}x{targetH}";
        var cachedFrame = _cache.GetFrame(cacheKey, frameIndex);
        if (cachedFrame != null)
        {
            return cachedFrame;
        }

        var decoded = await _decoder.DecodeFrameAsync(
            resolvedPath,
            frameIndex,
            targetW,
            targetH,
            _useGpuAcceleration,
            cancellationToken
        );

        _cache.AddFrame(cacheKey, frameIndex, decoded);
        return decoded;
    }

    public void Dispose()
    {
        _decoder.Dispose();
    }
}
