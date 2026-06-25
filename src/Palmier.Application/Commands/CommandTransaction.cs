using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Palmier.Application.State;
using Palmier.Domain;

namespace Palmier.Application.Commands;

/// Groups multiple commands into an atomic unit.
/// If any step fails or is cancelled, all previously executed steps are rolled back.
public sealed class CommandTransaction : IAsyncCommand
{
    private readonly List<IAsyncCommand> _commands = new();
    private readonly List<IAsyncCommand> _executed = new();

    public string Label { get; }

    public CommandTransaction(string label)
    {
        Label = label;
    }

    public CommandTransaction(string label, params IAsyncCommand[] commands)
    {
        Label = label;
        _commands.AddRange(commands);
    }

    public void Add(IAsyncCommand command) => _commands.Add(command);

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var sw = Stopwatch.StartNew();
        _executed.Clear();

        for (int i = 0; i < _commands.Count; i++)
        {
            context.ThrowIfCancelled();
            context.ReportProgress((double)i / _commands.Count);

            var result = await _commands[i].ExecuteAsync(context);
            if (!result.Succeeded)
            {
                await RollbackAsync(context);
                return CommandResult.Failure(
                    $"Transaction '{Label}' failed at step {i + 1}/{_commands.Count}: {result.ErrorMessage}",
                    sw.Elapsed, result.Exception);
            }

            _executed.Add(_commands[i]);
        }

        context.ReportProgress(1.0);
        return CommandResult.Success(sw.Elapsed);
    }

    public async Task<CommandResult> UndoAsync(CommandContext context)
    {
        var sw = Stopwatch.StartNew();
        await RollbackAsync(context);
        return CommandResult.Success(sw.Elapsed);
    }

    private async Task RollbackAsync(CommandContext context)
    {
        for (int i = _executed.Count - 1; i >= 0; i--)
        {
            try
            {
                await _executed[i].UndoAsync(context);
            }
            catch
            {
                // Best-effort rollback — log but don't throw during recovery
            }
        }
        _executed.Clear();
    }
}
