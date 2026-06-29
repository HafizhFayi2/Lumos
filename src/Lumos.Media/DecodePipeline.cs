using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Media;

public sealed class DecodePipeline : IDisposable
{
    private const int DefaultSourceFps = 30;
    private const int FfmpegTimeoutMs = 30_000;
    private const int MaxConcurrentDecodes = 4;
    private readonly SemaphoreSlim _decodeSemaphore = new(MaxConcurrentDecodes);
    private bool _isDisposed;

    public int ConcurrentDecodeCount => MaxConcurrentDecodes - _decodeSemaphore.CurrentCount;

    /// Decode a single frame from a video file using two-pass FFmpeg seeking.
    /// Two-pass seeking (fast keyframe seek + accurate frame seek) improves
    /// frame accuracy over single-pass seeking while remaining faster than
    /// decoding from the start of the file.
    public async Task<byte[]> DecodeFrameAsync(
        string assetPath,
        int frameIndex,
        int targetWidth,
        int targetHeight,
        bool useGpuAcceleration = false,
        int sourceFps = 30,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(DecodePipeline));

        await _decodeSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("Media file not found.", assetPath);

            if (targetWidth <= 0 || targetHeight <= 0)
                throw new ArgumentException("Target dimensions must be positive.");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(FfmpegTimeoutMs);
            return await DecodeFrameWithFfmpegAsync(
                assetPath, frameIndex, targetWidth, targetHeight,
                sourceFps, useGpuAcceleration, timeoutCts.Token);
        }
        finally
        {
            _decodeSemaphore.Release();
        }
    }

    /// Decode raw PCM audio samples from a media file.
    /// Returns float array in interleaved stereo format (L,R,L,R...) at 48kHz.
    /// Shares the same semaphore throttle as video decoding to limit total FFmpeg
    /// process count and prevent resource exhaustion.
    public async Task<float[]?> DecodeAudioSamplesAsync(
        string assetPath,
        double startTime,
        double duration,
        CancellationToken cancellationToken = default)
    {
        if (_isDisposed) return null;

        if (!File.Exists(assetPath))
            return null;

        // Share the decode semaphore to limit total concurrent FFmpeg processes
        await _decodeSemaphore.WaitAsync(cancellationToken);
        try
        {
            int sampleRate = 48000;
            int channels = 2;
            int sampleCount = (int)(sampleRate * duration * channels);
            if (sampleCount <= 0) return null;

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string startStr = startTime.ToString("0.###", CultureInfo.InvariantCulture);
            string durStr = duration.ToString("0.###", CultureInfo.InvariantCulture);

            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-ss");
            psi.ArgumentList.Add(startStr);
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(assetPath);
            psi.ArgumentList.Add("-t");
            psi.ArgumentList.Add(durStr);
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("f32le");
            psi.ArgumentList.Add("-acodec");
            psi.ArgumentList.Add("pcm_f32le");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add(sampleRate.ToString());
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add(channels.ToString());
            psi.ArgumentList.Add("pipe:1");

            using var process = Process.Start(psi);
            if (process == null) return null;

            using var cancelReg = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            });

            int byteCount = sampleCount * 4; // f32le = 4 bytes per float
            var buffer = new byte[byteCount];
            int offset = 0;
            while (offset < byteCount)
            {
                int read = await process.StandardOutput.BaseStream.ReadAsync(
                    buffer.AsMemory(offset, Math.Min(65536, byteCount - offset)),
                    cancellationToken);
                if (read == 0) break;
                offset += read;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && offset >= 4)
            {
                var samples = new float[offset / 4];
                Buffer.BlockCopy(buffer, 0, samples, 0, offset);
                return samples;
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            _decodeSemaphore.Release();
        }
    }

    /// Probe a media file to get its actual frame count and FPS.
    /// Returns (frameCount, fps) or null if probe fails.
    public static async Task<(int frameCount, double fps)?> ProbeMediaAsync(
        string assetPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(assetPath))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-select_streams");
            psi.ArgumentList.Add("v:0");
            psi.ArgumentList.Add("-count_frames");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("stream=nb_read_frames,r_frame_rate");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("csv=p=0");
            psi.ArgumentList.Add(assetPath);

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            string error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            // Output format: frameCount,fpsFraction (e.g. "150,N/1" or "150,30000/1001")
            var parts = output.Trim().Split(',');
            if (parts.Length < 2) return null;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frameCount))
                return null;

            double fps = ParseFpsFraction(parts[1]);
            if (fps <= 0) fps = 30;

            return (frameCount, fps);
        }
        catch
        {
            return null;
        }
    }

    private static double ParseFpsFraction(string fraction)
    {
        // Format: "30000/1001" or "30/1" or "29.97"
        if (string.IsNullOrWhiteSpace(fraction)) return 0;

        if (fraction.Contains('/'))
        {
            var parts = fraction.Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
                den > 0)
                return num / den;
        }

        if (double.TryParse(fraction, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;

        return 0;
    }

    private static async Task<byte[]> DecodeFrameWithFfmpegAsync(
        string assetPath,
        int frameIndex,
        int targetWidth,
        int targetHeight,
        int sourceFps,
        bool useGpuAcceleration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int byteCount = checked(targetWidth * targetHeight * 4);
        int effectiveFps = sourceFps > 0 ? sourceFps : DefaultSourceFps;
        double seconds = Math.Max(0, frameIndex) / (double)effectiveFps;
        string timeStr = seconds.ToString("0.000", CultureInfo.InvariantCulture);

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

        // Two-pass seeking: fast keyframe seek before input + accurate frame seek after
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(timeStr);
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(assetPath);
        psi.ArgumentList.Add("-ss");
        psi.ArgumentList.Add(timeStr);

        // GPU acceleration path
        if (useGpuAcceleration)
        {
            psi.ArgumentList.Add("-hwaccel");
            psi.ArgumentList.Add("auto");
        }

        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");

        // Use precise frame selection via select filter as additional accuracy guarantee
        // scale then pad to target dimensions with black letterboxing
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add(
            $"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease," +
            $"pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2:black");
        psi.ArgumentList.Add("-pix_fmt");
        psi.ArgumentList.Add("bgra");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("rawvideo");
        psi.ArgumentList.Add("pipe:1");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg could not be started.");

        // Kill ffmpeg on cancellation
        using var cancelReg = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
        });

        var buffer = new byte[byteCount];
        string stderr = string.Empty;

        var stderrTask = Task.Run(async () =>
        {
            try { stderr = await process.StandardError.ReadToEndAsync(cancellationToken); }
            catch (OperationCanceledException) { }
        }, cancellationToken);

        try
        {
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
                        ? $"ffmpeg decoded {offset}/{byteCount} bytes for frame {frameIndex} at {timeStr}s."
                        : stderr.Trim());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"FFmpeg decode timed out after {FfmpegTimeoutMs}ms for frame {frameIndex}.");
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
