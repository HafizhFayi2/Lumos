using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.Media;

public sealed class PlaybackCoordinator : IDisposable
{
    private readonly EditorStore _store;
    private readonly VideoEngine _videoEngine;
    private readonly IAudioProvider _audioProvider;
    private readonly IFrameCache _frameCache;

    private readonly ConcurrentDictionary<int, byte[]> _prefetchedFrames = new();
    private CancellationTokenSource? _bufferCts;
    private readonly object _bufferLock = new();

    public VideoEngine VideoEngine => _videoEngine;

    public PlaybackCoordinator(
        EditorStore store,
        VideoEngine videoEngine,
        IAudioProvider audioProvider,
        IFrameCache frameCache)
    {
        _store = store;
        _videoEngine = videoEngine;
        _audioProvider = audioProvider;
        _frameCache = frameCache;

        _store.StateChanged += OnStateChanged;
        _videoEngine.FrameComposited += OnFrameComposited;
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        if (e.ChangedField.HasFlag(StateField.Timeline))
        {
            _videoEngine.Rebuild();
            ClearBuffer();
            TriggerPrefetch();
        }
        else if (e.ChangedField.HasFlag(StateField.Playback))
        {
            var playback = _store.State.Playback;
            if (playback.IsPlaying)
            {
                StartBufferLoop();
            }
            else
            {
                StopBufferLoop();
            }
        }
    }

    private void OnFrameComposited(int frame, byte[] data)
    {
        int keepFramesBefore = 5;
        foreach (var key in _prefetchedFrames.Keys)
        {
            if (key < frame - keepFramesBefore)
            {
                _prefetchedFrames.TryRemove(key, out _);
            }
        }
        TriggerPrefetch();
    }

    private void StartBufferLoop()
    {
        lock (_bufferLock)
        {
            _bufferCts?.Cancel();
            _bufferCts = new CancellationTokenSource();
            _ = RunBufferLoopAsync(_bufferCts.Token);
        }
    }

    private void StopBufferLoop()
    {
        lock (_bufferLock)
        {
            _bufferCts?.Cancel();
            _bufferCts = null;
        }
    }

    private void ClearBuffer()
    {
        _prefetchedFrames.Clear();
    }

    private void TriggerPrefetch()
    {
        int currentFrame = _store.State.PlayheadFrame;
        int totalFrames = _store.State.TotalFrames;
        
        int prefetchCount = 15;
        for (int i = 1; i <= prefetchCount; i++)
        {
            int targetFrame = currentFrame + i;
            if (targetFrame >= totalFrames) break;
            
            if (!_prefetchedFrames.ContainsKey(targetFrame))
            {
                _ = PrefetchFrameAsync(targetFrame);
            }
        }
    }

    private async Task PrefetchFrameAsync(int frame)
    {
        try
        {
            if (!_prefetchedFrames.TryAdd(frame, Array.Empty<byte>())) return;

            var data = await _videoEngine.GetCompositedFrameAsync(frame, 1920, 1080);
            if (data != null)
            {
                _prefetchedFrames[frame] = data;
                _frameCache.AddFrame("composited_timeline", frame, data);
            }
            else
            {
                _prefetchedFrames.TryRemove(frame, out _);
            }
        }
        catch
        {
            _prefetchedFrames.TryRemove(frame, out _);
        }
    }

    private async Task RunBufferLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TriggerPrefetch();

            int currentFrame = _store.State.PlayheadFrame;
            int fps = _store.State.Fps > 0 ? _store.State.Fps : 30;
            double startTimeSec = (double)currentFrame / fps;
            double durationSec = 1.0 / fps;

            var timeline = _store.State.Timeline.Timeline;
            if (timeline != null)
            {
                foreach (var track in timeline.Tracks)
                {
                    if (track.IsMuted || track.Type != ClipType.Audio) continue;
                    foreach (var clip in track.Clips)
                    {
                        if (clip.Contains(currentFrame))
                        {
                            var samples = await _audioProvider.GetAudioSamplesAsync(
                                clip.MediaRef, 
                                startTimeSec, 
                                durationSec
                            );
                        }
                    }
                }
            }

            await Task.Delay(200, ct);
        }
    }

    public void Dispose()
    {
        _store.StateChanged -= OnStateChanged;
        _videoEngine.FrameComposited -= OnFrameComposited;
        StopBufferLoop();
        _videoEngine.Dispose();
    }
}
