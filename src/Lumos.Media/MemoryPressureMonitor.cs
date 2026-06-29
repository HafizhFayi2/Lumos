using System;
using System.Diagnostics;
using System.Runtime;

namespace Lumos.Media;

/// <summary>
/// Monitors system memory pressure and automatically trims <see cref="IFrameCache"/>
/// when the GC reports high memory load. This prevents the frame cache from
/// contributing to out-of-memory conditions during playback or export.
/// </summary>
public sealed class MemoryPressureMonitor : IDisposable
{
    private readonly IFrameCache _cache;
    private readonly TimeSpan _checkInterval;
    private readonly double _shrinkThreshold;
    private readonly double _targetRatio;
    private Timer? _timer;
    private bool _isDisposed;

    /// <summary>
    /// How many bytes the monitor has freed since it was created.
    /// </summary>
    public long TotalBytesFreed { get; private set; }

    /// <summary>
    /// Number of times the monitor triggered a shrink.
    /// </summary>
    public int ShrinkCount { get; private set; }

    /// <summary>
    /// Create a memory pressure monitor.
    /// </summary>
    /// <param name="cache">The frame cache to trim under pressure.</param>
    /// <param name="checkInterval">How often to check memory status.</param>
    /// <param name="shrinkThreshold">
    /// Usage ratio (0.0–1.0) above which the cache is considered full enough to trim.
    /// Default 0.7 = start trimming when cache is 70%+ full.
    /// </param>
    /// <param name="targetRatio">
    /// Target usage ratio after trimming. Default 0.4 = trim until 40% full.
    /// </param>
    public MemoryPressureMonitor(
        IFrameCache cache,
        TimeSpan? checkInterval = null,
        double shrinkThreshold = 0.7,
        double targetRatio = 0.4)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(5);
        _shrinkThreshold = Math.Clamp(shrinkThreshold, 0.1, 1.0);
        _targetRatio = Math.Clamp(targetRatio, 0.0, 1.0);
    }

    /// <summary>
    /// Start periodic memory pressure checks.
    /// </summary>
    public void Start()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(MemoryPressureMonitor));
        _timer?.Dispose();
        _timer = new Timer(OnTimer, null, _checkInterval, _checkInterval);
    }

    /// <summary>
    /// Stop periodic memory pressure checks.
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Manually check memory pressure and trim if needed. Can be called
    /// from anywhere, e.g. after a large allocation.
    /// </summary>
    public void CheckNow()
    {
        if (_isDisposed) return;
        CheckAndTrim();
    }

    private void OnTimer(object? state)
    {
        if (_isDisposed) return;
        CheckAndTrim();
    }

    private void CheckAndTrim()
    {
        try
        {
            if (_cache.Count == 0) return;

            var memInfo = GC.GetGCMemoryInfo();
            long committedBytes = GC.GetTotalMemory(false);
            long threshold = memInfo.HighMemoryLoadThresholdBytes;

            // Check if system is under memory pressure
            bool underPressure = threshold > 0 && committedBytes > threshold * 0.7;

            // Check if cache is above the shrink threshold
            bool cacheFull = _cache.UsageRatio >= _shrinkThreshold;

            if (underPressure || cacheFull)
            {
                long currentSize = _cache.CurrentSizeBytes;

                if (underPressure)
                {
                    // Under system pressure: shrink more aggressively
                    // Free half the cache
                    long targetFree = currentSize / 2;
                    long freed = _cache.Shrink(targetFree);
                    TotalBytesFreed += freed;
                    if (freed > 0) ShrinkCount++;
                    Debug.WriteLine($"[MemoryPressureMonitor] System pressure: freed {freed / 1024 / 1024} MB from frame cache");
                }
                else if (cacheFull)
                {
                    // Cache is just full: trim to target ratio
                    long targetSize = (long)(_cache.CurrentSizeBytes * _targetRatio / _cache.UsageRatio);
                    long bytesToFree = currentSize - targetSize;
                    if (bytesToFree > 0)
                    {
                        long freed = _cache.Shrink(bytesToFree);
                        TotalBytesFreed += freed;
                        if (freed > 0) ShrinkCount++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MemoryPressureMonitor] Check failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _timer?.Dispose();
    }
}
