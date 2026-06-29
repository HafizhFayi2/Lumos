using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.Media;

public sealed class VideoEngine : IFrameProvider, IDisposable
{
    private readonly EditorStore _store;
    private readonly IFrameProvider _frameProvider;
    private readonly ISeekController _seekController;
    private readonly IFrameCompositor? _frameCompositor;
    private readonly CompositionBuilder _composer = new();
    private readonly TimelineAudioPlayer _audioPlayer = new();
    private PreviewQuality _previewQuality = PreviewQuality.Half;

    private CancellationTokenSource? _playbackCts;
    private readonly object _playbackLock = new();

    public event Action<int, byte[]>? FrameComposited;

    public VideoEngine(EditorStore store, IFrameProvider frameProvider, ISeekController seekController, IFrameCompositor? frameCompositor = null)
    {
        _store = store;
        _frameProvider = frameProvider;
        _seekController = seekController;
        _frameCompositor = frameCompositor;
        
        var timeline = _store.State.Timeline.Timeline;
        if (timeline != null)
        {
            _composer.Load(timeline);
        }

        // Rebuild composition whenever timeline changes
        _store.StateChanged += (_, e) =>
        {
            if (e.ChangedField.HasFlag(StateField.Timeline))
                Rebuild();
            if (e.ChangedField.HasFlag(StateField.PreviewQuality))
                _previewQuality = _store.State.PreviewQuality;
        };
    }

    public void Rebuild()
    {
        var timeline = _store.State.Timeline.Timeline;
        if (timeline != null)
        {
            _composer.Load(timeline);
        }
    }

    public void Play()
    {
        Console.WriteLine($"[VideoEngine] Play() called. Playhead: {_store.State.Playback.PlayheadFrame}, TotalFrames: {_store.State.TotalFrames}");
        lock (_playbackLock)
        {
            if (_store.State.Playback.IsPlaying) return;

            _store.UpdatePlayback(p => p.Play());
            _audioPlayer.Play(_store.State.Timeline.Timeline, _store.State.Playback.PlayheadFrame);
            _playbackCts = new CancellationTokenSource();
            _ = PlaybackLoopAsync(_playbackCts.Token);
        }
    }

    public void Pause()
    {
        lock (_playbackLock)
        {
            if (!_store.State.Playback.IsPlaying) return;

            _store.UpdatePlayback(p => p.Pause());
            _audioPlayer.Stop();
            _playbackCts?.Cancel();
            _playbackCts = null;
        }
    }

    public void TogglePlayback()
    {
        if (_store.State.Playback.IsPlaying)
            Pause();
        else
            Play();
    }

    private (int width, int height) GetRenderSize()
    {
        var timeline = _store.State.Timeline.Timeline;
        int baseW = timeline?.Width > 0 ? timeline.Width : 1920;
        int baseH = timeline?.Height > 0 ? timeline.Height : 1080;
        return _previewQuality switch
        {
            PreviewQuality.Full => (baseW, baseH),
            PreviewQuality.Half => (Math.Max(1, baseW / 2), Math.Max(1, baseH / 2)),
            PreviewQuality.Quarter => (Math.Max(1, baseW / 4), Math.Max(1, baseH / 4)),
            _ => (Math.Max(1, baseW / 2), Math.Max(1, baseH / 2))
        };
    }

    private int SafeTotalFrames() => Math.Max(0, _store.State.Timeline?.Timeline?.TotalFrames ?? 0);

    public void Seek(int frame, bool isScrub = false)
    {
        int totalFrames = SafeTotalFrames();
        frame = Math.Clamp(frame, 0, Math.Max(0, totalFrames - 1));
        
        _store.UpdatePlayback(p => p.SetPlayhead(frame, totalFrames));
        if (_store.State.Playback.IsPlaying)
            _audioPlayer.Play(_store.State.Timeline.Timeline, frame);
        
        var (w, h) = GetRenderSize();
        _seekController.EnqueueSeek(frame, async f =>
        {
            try
            {
                var pixelData = await GetCompositedFrameAsync(f, w, h);
                if (pixelData != null)
                {
                    FrameComposited?.Invoke(f, pixelData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoEngine] Seek composite failed: {ex.Message}");
            }
        });
    }

    public async Task<byte[]?> GetFrameAsync(string assetPath, int sourceFrame, int width, int height)
    {
        return await GetCompositedFrameAsync(sourceFrame, width, height);
    }

    public async Task<byte[]?> GetCompositedFrameAsync(int frame, int width, int height)
    {
        var compFrame = _composer.Build(frame);
        if (compFrame == null || compFrame.Visual.Count == 0)
        {
            return new byte[width * height * 4]; // Black frame
        }

        if (_frameCompositor != null)
        {
            return await _frameCompositor.CompositeAsync(compFrame);
        }

        return await MediaCompositor.CompositeSlotsAsync(compFrame.Visual, _frameProvider, width, height);
    }

    public async Task<(float[] y, float[] r, float[] g, float[] b)?> GetHistogramYRGBAsync(int? frameNumber = null, int count = 256)
    {
        int frame = frameNumber ?? _store.State.PlayheadFrame;
        var pixelData = await GetCompositedFrameAsync(frame, 320, 180);
        if (pixelData == null) return null;

        float[] y = new float[count];
        float[] r = new float[count];
        float[] g = new float[count];
        float[] b = new float[count];

        for (int i = 0; i < pixelData.Length; i += 4)
        {
            float pb = pixelData[i] / 255f;
            float pg = pixelData[i + 1] / 255f;
            float pr = pixelData[i + 2] / 255f;

            float py = 0.2126f * pr + 0.7152f * pg + 0.0722f * pb;

            int yBin = Math.Clamp((int)(py * (count - 1)), 0, count - 1);
            int rBin = Math.Clamp((int)(pr * (count - 1)), 0, count - 1);
            int gBin = Math.Clamp((int)(pg * (count - 1)), 0, count - 1);
            int bBin = Math.Clamp((int)(pb * (count - 1)), 0, count - 1);

            y[yBin]++;
            r[rBin]++;
            g[gBin]++;
            b[bBin]++;
        }

        float maxV = 0f;
        for (int i = 0; i < count; i++)
        {
            maxV = Math.Max(maxV, Math.Max(y[i], Math.Max(r[i], Math.Max(g[i], b[i]))));
        }

        if (maxV > 0f)
        {
            for (int i = 0; i < count; i++)
            {
                y[i] /= maxV;
                r[i] /= maxV;
                g[i] /= maxV;
                b[i] /= maxV;
            }
        }

        return (y, r, g, b);
    }

    public async Task<float[]?> GetHueHistogramAsync(int? frameNumber = null, int count = 96)
    {
        int frame = frameNumber ?? _store.State.PlayheadFrame;
        var pixelData = await GetCompositedFrameAsync(frame, 320, 180);
        if (pixelData == null) return null;

        float[] bins = new float[count];

        for (int i = 0; i < pixelData.Length; i += 4)
        {
            float pb = pixelData[i] / 255f;
            float pg = pixelData[i + 1] / 255f;
            float pr = pixelData[i + 2] / 255f;

            float mx = Math.Max(pr, Math.Max(pg, pb));
            float mn = Math.Min(pr, Math.Min(pg, pb));
            float d = mx - mn;

            if (d <= 1e-4f || mx <= 1e-4f) continue;

            float hue = mx == pr ? (pg - pb) / d : (mx == pg ? (pb - pr) / d + 2f : (pr - pg) / d + 4f);
            hue = (hue / 6f) % 1f;
            if (hue < 0f) hue += 1f;

            int binIdx = Math.Min(count - 1, (int)(hue * count));
            bins[binIdx] += d / mx;
        }

        float maxV = bins.Max();
        if (maxV > 0f)
        {
            for (int i = 0; i < count; i++)
            {
                bins[i] = (float)Math.Sqrt(bins[i] / maxV);
            }
        }

        return bins;
    }

    private async Task PlaybackLoopAsync(CancellationToken ct)
    {
        int fps = Math.Max(1, _store.State.Fps > 0 ? _store.State.Fps : 30);
        int frameDurationMs = 1000 / fps;
        var sw = new Stopwatch();

        Debug.WriteLine($"[VideoEngine] Playback loop started. FPS: {fps}");

        while (!ct.IsCancellationRequested)
        {
            sw.Restart();

            int totalFrames = SafeTotalFrames();
            if (totalFrames <= 0)
            {
                _store.UpdatePlayback(p => p.SetPlayhead(0, 0));
                Pause();
                break;
            }

            int currentFrame = Math.Max(0, _store.State.PlayheadFrame);

            if (currentFrame >= totalFrames - 1)
            {
                _store.UpdatePlayback(p => p.SetPlayhead(totalFrames - 1, totalFrames));
                Pause();
                break;
            }

            int nextFrame = currentFrame + 1;
            _store.UpdatePlayback(p => p.SetPlayhead(nextFrame, totalFrames));

            // Audio sync (non-blocking on failure)
            try
            {
                var tl = _store.State.Timeline.Timeline;
                _audioPlayer.Sync(tl, nextFrame);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoEngine] Audio sync failed at frame {nextFrame}: {ex.Message}");
            }

            // Render frame
            try
            {
                var (w, h) = GetRenderSize();
                var pixelData = await GetCompositedFrameAsync(nextFrame, w, h);
                if (pixelData != null)
                {
                    FrameComposited?.Invoke(nextFrame, pixelData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoEngine] Frame composite failed at frame {nextFrame}: {ex.Message}");
            }

            // Frame-rate regulation using Stopwatch (avoids DateTime drift)
            long elapsedMs = sw.ElapsedMilliseconds;
            int delay = (int)Math.Max(0, frameDurationMs - elapsedMs);

            if (delay > 0)
            {
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        Pause();
        _audioPlayer.Dispose();
        if (_frameCompositor is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
