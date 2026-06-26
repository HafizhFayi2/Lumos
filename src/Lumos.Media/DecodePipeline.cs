using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Media;

public sealed class DecodePipeline : IDisposable
{
    private const int DefaultSourceFps = 30;
    private readonly SemaphoreSlim _decodeSemaphore = new(2);
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
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("Media file not found.", assetPath);

            return await DecodeFrameWithFfmpegAsync(
                assetPath,
                frameIndex,
                targetWidth,
                targetHeight,
                cancellationToken);
        }
        finally
        {
            _decodeSemaphore.Release();
        }
    }

    private static async Task<byte[]> DecodeFrameWithFfmpegAsync(
        string assetPath,
        int frameIndex,
        int targetWidth,
        int targetHeight,
        CancellationToken cancellationToken)
    {
        int byteCount = checked(targetWidth * targetHeight * 4);
        var buffer = new byte[byteCount];
        double seconds = Math.Max(0, frameIndex) / (double)DefaultSourceFps;

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(assetPath);
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add($"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease,pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2:black");
        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("bgra");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("rawvideo");
        psi.ArgumentList.Add("pipe:1");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg could not be started.");

        using var _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        });

        string stderr = string.Empty;
        var stderrTask = Task.Run(async () =>
        {
            stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        }, cancellationToken);

        int offset = 0;
        while (offset < byteCount)
        {
            int read = await process.StandardOutput.BaseStream.ReadAsync(
                buffer.AsMemory(offset, byteCount - offset),
                cancellationToken);
            if (read == 0) break;
            offset += read;
        }

        await process.WaitForExitAsync(cancellationToken);
        await stderrTask;

        if (process.ExitCode != 0 || offset < byteCount)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(stderr)
                    ? $"ffmpeg decoded {offset}/{byteCount} bytes."
                    : stderr.Trim());
        }

        return buffer;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _decodeSemaphore.Dispose();
    }
}
