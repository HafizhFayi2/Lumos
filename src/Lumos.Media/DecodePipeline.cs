using System;
using System.Threading;
using System.Threading.Tasks;

namespace Palmier.Media;

public sealed class DecodePipeline : IDisposable
{
    private readonly SemaphoreSlim _decodeSemaphore = new(4);
    private bool _isDisposed;

    public async Task<byte[]> DecodeFrameAsync(
        string assetPath,
        int frameIndex,
        int targetWidth,
        int targetHeight,
        bool useGpuAcceleration = false,
        CancellationToken cancellationToken = default)
    {
        await _decodeSemaphore.WaitAsync(cancellationToken);
        try
        {
            bool is4K = targetWidth >= 3840 || targetHeight >= 2160;
            int simulatedDelayMs = is4K ? 15 : 5;
            if (useGpuAcceleration)
            {
                simulatedDelayMs = Math.Max(1, simulatedDelayMs / 3);
            }
            await Task.Delay(simulatedDelayMs, cancellationToken);

            int bufferSize = targetWidth * targetHeight * 4;
            byte[] buffer = new byte[bufferSize];

            const int checkSize = 64;
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int index = (y * targetWidth + x) * 4;
                    bool isWhite = ((x / checkSize) + (y / checkSize) + frameIndex / 2) % 2 == 0;
                    byte colorVal = (byte)(isWhite ? 220 : 40);

                    buffer[index] = colorVal;
                    buffer[index + 1] = colorVal;
                    buffer[index + 2] = colorVal;
                    buffer[index + 3] = 255;
                }
            }

            return buffer;
        }
        finally
        {
            _decodeSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _decodeSemaphore.Dispose();
    }
}
