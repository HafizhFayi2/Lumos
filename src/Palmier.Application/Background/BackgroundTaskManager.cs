using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Palmier.Application.Background;

public sealed class BackgroundTaskManager : IDisposable
{
    private readonly TaskSchedulerService _scheduler;
    private readonly ConcurrentDictionary<string, (BackgroundJob Job, CancellationTokenSource Cts)> _activeJobs = new();

    public BackgroundTaskManager(int maxConcurrency = 4)
    {
        _scheduler = new TaskSchedulerService(maxConcurrency);
    }

    public Task<bool> SubmitJobAsync(
        string name,
        JobPriority priority,
        Func<CancellationToken, IProgress<double>, Task> action,
        IProgress<double>? progress = null)
    {
        string jobId = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<bool>();

        var job = new BackgroundJob(jobId, name, priority, action, tcs, progress);
        _activeJobs[jobId] = (job, cts);

        tcs.Task.ContinueWith(t => _activeJobs.TryRemove(jobId, out _), TaskScheduler.Default);

        _scheduler.ScheduleJobAsync(job);
        return tcs.Task;
    }

    public bool CancelJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var active))
        {
            active.Cts.Cancel();
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        foreach (var active in _activeJobs.Values)
        {
            active.Cts.Cancel();
            active.Cts.Dispose();
        }
        _activeJobs.Clear();
        _scheduler.Dispose();
    }
}
