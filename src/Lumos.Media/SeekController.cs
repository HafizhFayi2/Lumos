using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lumos.Media;

public interface ISeekController
{
    void EnqueueSeek(int frame, Action<int> seekAction);
    void Reset();
}

public class SeekController : ISeekController
{
    private readonly Stopwatch _throttleStopwatch = Stopwatch.StartNew();
    private const long ThrottleMs = 33; 
    private int? _pendingSeekFrame;
    private Action<int>? _pendingSeekAction;
    private bool _isProcessing;

    public void EnqueueSeek(int frame, Action<int> seekAction)
    {
        _pendingSeekFrame = frame;
        _pendingSeekAction = seekAction;

        if (!_isProcessing)
        {
            _isProcessing = true;
            ProcessSeekAsync();
        }
    }

    private async void ProcessSeekAsync()
    {
        while (_pendingSeekFrame.HasValue)
        {
            long elapsed = _throttleStopwatch.ElapsedMilliseconds;
            if (elapsed < ThrottleMs)
            {
                await Task.Delay((int)(ThrottleMs - elapsed));
            }

            if (_pendingSeekFrame.HasValue && _pendingSeekAction != null)
            {
                int frame = _pendingSeekFrame.Value;
                var action = _pendingSeekAction;

                _pendingSeekFrame = null;
                _pendingSeekAction = null;
                _throttleStopwatch.Restart();

                try
                {
                    action(frame);
                }
                catch
                {
                    // Ignore errors in UI callbacks
                }
            }
        }
        _isProcessing = false;
    }

    public void Reset()
    {
        _pendingSeekFrame = null;
        _pendingSeekAction = null;
    }
}
