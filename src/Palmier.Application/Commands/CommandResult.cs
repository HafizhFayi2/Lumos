using System;

namespace Palmier.Application.Commands;

/// Outcome of a command execution.
public sealed class CommandResult
{
    public bool Succeeded { get; }
    public bool WasCancelled { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }
    public TimeSpan Elapsed { get; }

    private CommandResult(bool succeeded, bool cancelled, string? error, Exception? ex, TimeSpan elapsed)
    {
        Succeeded    = succeeded;
        WasCancelled = cancelled;
        ErrorMessage = error;
        Exception    = ex;
        Elapsed      = elapsed;
    }

    public static CommandResult Success(TimeSpan elapsed) =>
        new(true, false, null, null, elapsed);

    public static CommandResult Failure(string message, TimeSpan elapsed, Exception? ex = null) =>
        new(false, false, message, ex, elapsed);

    public static CommandResult Cancelled(TimeSpan elapsed) =>
        new(false, true, "Cancelled", null, elapsed);
}
