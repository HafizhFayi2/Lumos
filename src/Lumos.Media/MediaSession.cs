using System;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Media;

public sealed class MediaSession : IDisposable
{
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly MediaPipeline _pipeline;
    private readonly IFrameCache _cache;
    private readonly PlaybackCoordinator _playbackCoordinator;
    private bool _isDisposed;

    public string SessionId => _sessionId;
    public MediaPipeline Pipeline => _pipeline;
    public IFrameCache Cache => _cache;
    public PlaybackCoordinator Playback => _playbackCoordinator;

    public MediaSession(
        MediaPipeline pipeline,
        IFrameCache cache,
        PlaybackCoordinator playbackCoordinator)
    {
        _pipeline = pipeline;
        _cache = cache;
        _playbackCoordinator = playbackCoordinator;
    }

    public async Task SeekAndPrefetchAsync(
        string assetPath,
        int centerFrame,
        int width,
        int height,
        int prefetchRadius = 5,
        CancellationToken cancellationToken = default)
    {
        await _pipeline.GetFrameAsync(assetPath, centerFrame, width, height, cancellationToken);

        for (int i = 1; i <= prefetchRadius; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            int nextFrame = centerFrame + i;
            int prevFrame = centerFrame - i;

            _ = _pipeline.GetFrameAsync(assetPath, nextFrame, width, height, CancellationToken.None);
            if (prevFrame >= 0)
            {
                _ = _pipeline.GetFrameAsync(assetPath, prevFrame, width, height, CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _pipeline.Dispose();
        _cache.Clear();
    }
}
