using System;

namespace Palmier.Application.Background;

public sealed class ProgressTracker : IProgress<double>
{
    private readonly Action<double>? _onProgress;
    private double _currentProgress;

    public double CurrentProgress => _currentProgress;

    public event EventHandler<double>? ProgressChanged;

    public ProgressTracker(Action<double>? onProgress = null)
    {
        _onProgress = onProgress;
    }

    public void Report(double value)
    {
        _currentProgress = Math.Clamp(value, 0.0, 1.0);
        _onProgress?.Invoke(_currentProgress);
        ProgressChanged?.Invoke(this, _currentProgress);
    }
}
