using System;
using System.Threading;
using Lumos.Application.State;

namespace Lumos.Application;

/// <summary>
/// Periodically saves the project when it has unsaved changes.
/// Runs on a background timer and writes to the project directory.
/// </summary>
public sealed class AutosaveService : IDisposable
{
    private readonly EditorStore _store;
    private readonly ProjectSerializer _serializer = new();
    private Timer? _timer;
    private string? _projectDir;
    private bool _disposed;

    public event Action? Saved;

    public AutosaveService(EditorStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Start autosaving for the given project directory.
    /// </summary>
    public void Start(string projectDir, int intervalMs = 30_000)
    {
        Stop();
        _projectDir = projectDir;
        _timer = new Timer(OnTick, null, intervalMs, intervalMs);
    }

    /// <summary>
    /// Stop autosaving.
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Perform a single save right now (e.g. on application close).
    /// </summary>
    public void SaveNow()
    {
        if (_projectDir == null) return;
        SaveToDisk();
    }

    private void OnTick(object? state)
    {
        if (_projectDir == null) return;
        if (!_store.State.IsDirty) return;
        SaveToDisk();
    }

    private void SaveToDisk()
    {
        try
        {
            var state = _store.State;
            var data = ProjectDataBuilder.BuildFromState(state);
            _serializer.Save(_projectDir!, data);
            _store.MarkClean();
            Saved?.Invoke();
        }
        catch
        {
            // Autosave failures should not crash the app.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
