using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Lumos.Application.Background;

public enum JobPriority { Low, Medium, High, Critical }

public record BackgroundJob(
    string Id,
    string Name,
    JobPriority Priority,
    Func<CancellationToken, IProgress<double>, Task> Action,
    TaskCompletionSource<bool> CompletionSource,
    IProgress<double>? Progress = null
);

public sealed class JobQueue
{
    private readonly SortedSet<BackgroundJob> _queue = new(new BackgroundJobComparer());
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _queue.Count; }
    }

    public void Enqueue(BackgroundJob job)
    {
        lock (_lock)
        {
            _queue.Add(job);
        }
    }

    public bool TryDequeue(out BackgroundJob? job)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                job = null;
                return false;
            }

            job = _queue.Min;
            _queue.Remove(job!);
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }

    private sealed class BackgroundJobComparer : IComparer<BackgroundJob>
    {
        public int Compare(BackgroundJob? x, BackgroundJob? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return 1;
            if (y is null) return -1;

            int priorityCompare = y.Priority.CompareTo(x.Priority);
            if (priorityCompare != 0) return priorityCompare;

            return string.Compare(x.Id, y.Id, StringComparison.Ordinal);
        }
    }
}
