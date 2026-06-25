using System;
using System.Collections.Generic;
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
    private readonly CompositionBuilder _composer = new();

    private CancellationTokenSource? _playbackCts;
    private readonly object _playbackLock = new();

    public event Action<int, byte[]>? FrameComposited;

    public VideoEngine(EditorStore store, IFrameProvider frameProvider, ISeekController seekController)
    {
        _store = store;
        _frameProvider = frameProvider;
        _seekController = seekController;
        
        var timeline = _store.State.Timeline.Timeline;
        if (timeline != null)
        {
            _composer.Load(timeline);
        }
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
        lock (_playbackLock)
        {
            if (_store.State.Playback.IsPlaying) return;

            _store.UpdatePlayback(p => p.Play());
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

    public void Seek(int frame, bool isScrub = false)
    {
        var totalFrames = _store.State.TotalFrames;
        _store.UpdatePlayback(p => p.SetPlayhead(frame, totalFrames));
        
        _seekController.EnqueueSeek(frame, async f =>
        {
            var pixelData = await GetCompositedFrameAsync(f, 1920, 1080);
            if (pixelData != null)
            {
                FrameComposited?.Invoke(f, pixelData);
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

        byte[] dest = new byte[width * height * 4];

        // Composite in painter's order (CompositionBuilder.Build visual list is bottom-to-top)
        foreach (var slot in compFrame.Visual)
        {
            var src = await _frameProvider.GetFrameAsync(slot.AssetPath, slot.SourceFrame, width, height);
            if (src == null) continue;

            double opacity = slot.Opacity;
            if (opacity <= 0) continue;

            if (opacity >= 1.0)
            {
                // Direct copy for opaque slots (faster)
                Buffer.BlockCopy(src, 0, dest, 0, src.Length);
            }
            else
            {
                // Software alpha blend
                for (int i = 0; i < dest.Length; i += 4)
                {
                    byte sb = src[i];
                    byte sg = src[i + 1];
                    byte sr = src[i + 2];
                    byte sa = src[i + 3];

                    double a = (sa / 255.0) * opacity;
                    if (a <= 0) continue;

                    byte db = dest[i];
                    byte dg = dest[i + 1];
                    byte dr = dest[i + 2];
                    byte da = dest[i + 3];

                    dest[i]     = (byte)(sb * a + db * (1.0 - a));
                    dest[i + 1] = (byte)(sg * a + dg * (1.0 - a));
                    dest[i + 2] = (byte)(sr * a + dr * (1.0 - a));
                    dest[i + 3] = (byte)(Math.Max(da, sa * opacity));
                }
            }
        }

        return dest;
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
        int fps = _store.State.Fps > 0 ? _store.State.Fps : 30;
        int frameDurationMs = 1000 / fps;

        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;

            int currentFrame = _store.State.PlayheadFrame;
            int totalFrames = _store.State.TotalFrames;

            if (currentFrame >= totalFrames)
            {
                // Reached the end
                _store.UpdatePlayback(p => p.SetPlayhead(0, totalFrames));
                Pause();
                break;
            }

            int nextFrame = currentFrame + 1;
            _store.UpdatePlayback(p => p.SetPlayhead(nextFrame, totalFrames));

            // Async render next frame
            var pixelData = await GetCompositedFrameAsync(nextFrame, 1920, 1080);
            if (pixelData != null)
            {
                FrameComposited?.Invoke(nextFrame, pixelData);
            }

            var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
            int delay = (int)Math.Max(0, frameDurationMs - elapsed);

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
    }
}
