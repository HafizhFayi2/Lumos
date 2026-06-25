using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lumos.Application.State;

namespace Lumos.Application.Commands;

/// Serialized async command queue with undo/redo.
/// All commands are funneled through a single background consumer
/// to prevent concurrent timeline mutations.
public sealed class CommandQueue : IDisposable
{
    private readonly EditorStore _store;
    private readonly Channel<QueuedCommand> _channel;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _consumer;

    private readonly List<IAsyncCommand> _undoStack = new();
    private readonly List<IAsyncCommand> _redoStack = new();
    private readonly object _historyLock = new();

    public int UndoCount { get { lock (_historyLock) return _undoStack.Count; } }
    public int RedoCount { get { lock (_historyLock) return _redoStack.Count; } }
    public bool CanUndo  { get { lock (_historyLock) return _undoStack.Count > 0; } }
    public bool CanRedo  { get { lock (_historyLock) return _redoStack.Count > 0; } }

    /// Raised after each command completes (success or failure).
    public event EventHandler<CommandCompletedEventArgs>? CommandCompleted;

    public CommandQueue(EditorStore store)
    {
        _store    = store;
        _channel  = Channel.CreateUnbounded<QueuedCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _consumer = Task.Run(() => ConsumeAsync(_lifetime.Token));
    }

    // ── Enqueue ─────────────────────────────────────────────────────────────

    /// Submit a command for async execution. Returns a Task that completes
    /// when the command finishes.
    public Task<CommandResult> EnqueueAsync(
        IAsyncCommand command,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null,
        Action<string>? statusCallback = null)
    {
        var tcs = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = new QueuedCommand(command, tcs, cancellationToken, progress, statusCallback);
        _channel.Writer.TryWrite(queued);
        return tcs.Task;
    }

    /// Fire-and-forget convenience for UI-triggered commands.
    public void Enqueue(
        IAsyncCommand command,
        CancellationToken cancellationToken = default)
    {
        _ = EnqueueAsync(command, cancellationToken);
    }

    // ── Undo / Redo ─────────────────────────────────────────────────────────

    public Task<CommandResult> UndoAsync(CancellationToken ct = default)
    {
        IAsyncCommand? command;
        lock (_historyLock)
        {
            if (_undoStack.Count == 0)
                return Task.FromResult(CommandResult.Failure("Nothing to undo.", TimeSpan.Zero));
            command = _undoStack[^1];
        }
        return EnqueueAsync(new UndoWrapper(command, this), ct);
    }

    public Task<CommandResult> RedoAsync(CancellationToken ct = default)
    {
        IAsyncCommand? command;
        lock (_historyLock)
        {
            if (_redoStack.Count == 0)
                return Task.FromResult(CommandResult.Failure("Nothing to redo.", TimeSpan.Zero));
            command = _redoStack[^1];
        }
        return EnqueueAsync(new RedoWrapper(command, this), ct);
    }

    // ── Consumer loop ───────────────────────────────────────────────────────

    private async Task ConsumeAsync(CancellationToken lifetime)
    {
        await foreach (var queued in _channel.Reader.ReadAllAsync(lifetime))
        {
            CommandResult result;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, queued.CancellationToken);
                var context = new CommandContext(_store, linked.Token, queued.Progress, queued.StatusCallback);

                result = await queued.Command.ExecuteAsync(context);

                if (result.Succeeded && queued.Command is not UndoWrapper && queued.Command is not RedoWrapper)
                {
                    PushToUndoStack(queued.Command);
                }
            }
            catch (OperationCanceledException)
            {
                result = CommandResult.Cancelled(TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                result = CommandResult.Failure(ex.Message, TimeSpan.Zero, ex);
            }

            queued.Completion.TrySetResult(result);
            CommandCompleted?.Invoke(this, new CommandCompletedEventArgs(queued.Command, result));
        }
    }

    private void PushToUndoStack(IAsyncCommand command)
    {
        lock (_historyLock)
        {
            // Coalesce if possible
            if (_undoStack.Count > 0 && command.CanMergeWith(_undoStack[^1]))
            {
                _undoStack[^1] = command.MergeWith(_undoStack[^1]);
            }
            else
            {
                _undoStack.Add(command);
            }

            _redoStack.Clear();

            const int MaxUndo = 200;
            if (_undoStack.Count > MaxUndo)
                _undoStack.RemoveAt(0);
        }
    }

    // ── Undo/Redo wrapper commands ──────────────────────────────────────────

    private sealed class UndoWrapper : IAsyncCommand
    {
        private readonly IAsyncCommand _target;
        private readonly CommandQueue _queue;

        public string Label => $"Undo: {_target.Label}";

        public UndoWrapper(IAsyncCommand target, CommandQueue queue)
        {
            _target = target;
            _queue  = queue;
        }

        public async Task<CommandResult> ExecuteAsync(CommandContext context)
        {
            var result = await _target.UndoAsync(context);
            if (result.Succeeded)
            {
                lock (_queue._historyLock)
                {
                    if (_queue._undoStack.Count > 0 && _queue._undoStack[^1] == _target)
                        _queue._undoStack.RemoveAt(_queue._undoStack.Count - 1);
                    _queue._redoStack.Add(_target);
                }
            }
            return result;
        }

        public Task<CommandResult> UndoAsync(CommandContext context) =>
            Task.FromResult(CommandResult.Failure("Cannot undo an undo wrapper.", TimeSpan.Zero));
    }

    private sealed class RedoWrapper : IAsyncCommand
    {
        private readonly IAsyncCommand _target;
        private readonly CommandQueue _queue;

        public string Label => $"Redo: {_target.Label}";

        public RedoWrapper(IAsyncCommand target, CommandQueue queue)
        {
            _target = target;
            _queue  = queue;
        }

        public async Task<CommandResult> ExecuteAsync(CommandContext context)
        {
            var result = await _target.ExecuteAsync(context);
            if (result.Succeeded)
            {
                lock (_queue._historyLock)
                {
                    if (_queue._redoStack.Count > 0 && _queue._redoStack[^1] == _target)
                        _queue._redoStack.RemoveAt(_queue._redoStack.Count - 1);
                    _queue._undoStack.Add(_target);
                }
            }
            return result;
        }

        public Task<CommandResult> UndoAsync(CommandContext context) =>
            Task.FromResult(CommandResult.Failure("Cannot undo a redo wrapper.", TimeSpan.Zero));
    }

    // ── Disposal ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

// ── Supporting types ────────────────────────────────────────────────────────

internal sealed record QueuedCommand(
    IAsyncCommand Command,
    TaskCompletionSource<CommandResult> Completion,
    CancellationToken CancellationToken,
    IProgress<double>? Progress,
    Action<string>? StatusCallback
);

public sealed class CommandCompletedEventArgs : EventArgs
{
    public IAsyncCommand Command { get; }
    public CommandResult Result { get; }

    public CommandCompletedEventArgs(IAsyncCommand command, CommandResult result)
    {
        Command = command;
        Result  = result;
    }
}
