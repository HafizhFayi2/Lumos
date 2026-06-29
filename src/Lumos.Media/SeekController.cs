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
    private readonly object _seekLock = new();

    public void EnqueueSeek(int frame, Action<int> seekAction)
    {
        if (seekAction == null) return;
        lock (_seekLock)
        {
            _pendingSeekFrame = frame;
            _pendingSeekAction = seekAction;

            if (!_isProcessing)
            {
                _isProcessing = true;
                _ = ProcessSeekAsync();
            }
        }
    }

    private async Task ProcessSeekAsync()
    {
        while (true)
        {
            int frame;
            Action<int> action;

            lock (_seekLock)
            {
                if (!_pendingSeekFrame.HasValue || _pendingSeekAction == null)
                {
                    _isProcessing = false;
                    return;
                }
                frame = _pendingSeekFrame.Value;
                action = _pendingSeekAction;
                _pendingSeekFrame = null;
                _pendingSeekAction = null;
            }

            long elapsed = _throttleStopwatch.ElapsedMilliseconds;
            if (elapsed < ThrottleMs)
            {
                await Task.Delay((int)(ThrottleMs - elapsed));
            }

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

    public void Reset()
    {
        lock (_seekLock)
        {
            _pendingSeekFrame = null;
            _pendingSeekAction = null;
        }
    }
}
