using System;
using System.Threading;
using Lumos.Application.State;

namespace Lumos.Application.Commands;

/// Execution context passed to every command.
/// Provides access to the editor state, cancellation, and progress reporting.
public sealed class CommandContext
{
    public EditorStore Store { get; }
    public CancellationToken CancellationToken { get; }

    private readonly IProgress<double>? _progress;
    private readonly Action<string>? _statusCallback;

    public CommandContext(
        EditorStore store,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null,
        Action<string>? statusCallback = null)
    {
        Store             = store;
        CancellationToken = cancellationToken;
        _progress         = progress;
        _statusCallback   = statusCallback;
    }

    /// Report progress as 0.0–1.0.
    public void ReportProgress(double fraction) =>
        _progress?.Report(Math.Clamp(fraction, 0, 1));

    /// Report a status message for the UI.
    public void ReportStatus(string message) =>
        _statusCallback?.Invoke(message);

    /// Throw if cancellation has been requested.
    public void ThrowIfCancelled() =>
        CancellationToken.ThrowIfCancellationRequested();

    /// Create a child context sharing the same store but with its own cancellation scope.
    public CommandContext WithCancellation(CancellationToken token) =>
        new(Store, token, _progress, _statusCallback);
}
