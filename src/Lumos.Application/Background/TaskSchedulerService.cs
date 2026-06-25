using System;
using System.Threading;
using System.Threading.Tasks;

namespace Palmier.Application.Background;

public sealed class TaskSchedulerService : IDisposable
{
    private readonly JobQueue _jobQueue = new();
    private readonly WorkerPool _workerPool;

    public TaskSchedulerService(int maxConcurrency)
    {
        _workerPool = new WorkerPool(_jobQueue, maxConcurrency);
    }

    public Task<bool> ScheduleJobAsync(BackgroundJob job)
    {
        _jobQueue.Enqueue(job);
        _workerPool.SignalNewJob();
        return job.CompletionSource.Task;
    }

    public void Dispose()
    {
        _workerPool.Dispose();
    }
}
