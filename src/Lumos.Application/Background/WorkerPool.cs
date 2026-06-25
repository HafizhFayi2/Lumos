using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Application.Background;

public sealed class WorkerPool : IDisposable
{
    private readonly JobQueue _jobQueue;
    private readonly List<Task> _workers = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _signal = new(0);
    private bool _isDisposed;

    public WorkerPool(JobQueue jobQueue, int workerCount)
    {
        _jobQueue = jobQueue;
        for (int i = 0; i < workerCount; i++)
        {
            _workers.Add(Task.Run(() => WorkerLoopAsync(_cts.Token)));
        }
    }

    public void SignalNewJob()
    {
        _signal.Release();
    }

    private async Task WorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_jobQueue.TryDequeue(out var job) && job != null)
            {
                var progressTracker = new ProgressTracker(p => job.Progress?.Report(p));
                try
                {
                    await job.Action(token, progressTracker);
                    job.CompletionSource.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    job.CompletionSource.TrySetCanceled(token);
                }
                catch (Exception ex)
                {
                    job.CompletionSource.TrySetException(ex);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _signal.Dispose();
    }
}
