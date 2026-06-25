using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Lumos.Application.State;
using Lumos.Domain;

namespace Lumos.Application.Commands;

/// Base class for commands that mutate the timeline via snapshot/restore.
/// Subclasses implement Apply() which receives the mutable Timeline.
/// Undo is handled automatically via snapshot.
public abstract class TimelineCommand : IAsyncCommand
{
    public abstract string Label { get; }

    public virtual bool CanMergeWith(IAsyncCommand previous) => false;
    public virtual IAsyncCommand MergeWith(IAsyncCommand previous) => this;

    private Timeline? _beforeSnapshot;

    public Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            context.ThrowIfCancelled();

            context.Store.MutateTimeline(Label, timeline =>
            {
                _beforeSnapshot = timeline.Clone();
                Apply(timeline, context);
            });

            context.ReportProgress(1.0);
            return Task.FromResult(CommandResult.Success(sw.Elapsed));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(CommandResult.Cancelled(sw.Elapsed));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failure(ex.Message, sw.Elapsed, ex));
        }
    }

    public Task<CommandResult> UndoAsync(CommandContext context)
    {
        var sw = Stopwatch.StartNew();
        if (_beforeSnapshot is null)
            return Task.FromResult(CommandResult.Failure("No snapshot to restore.", sw.Elapsed));

        context.Store.MutateTimeline($"Undo {Label}", timeline =>
        {
            RestoreFrom(_beforeSnapshot, timeline);
        });

        return Task.FromResult(CommandResult.Success(sw.Elapsed));
    }

    /// Apply the command's mutation to the timeline.
    protected abstract void Apply(Timeline timeline, CommandContext context);

    /// Copy all state from source into target.
    private static void RestoreFrom(Timeline source, Timeline target)
    {
        target.Fps               = source.Fps;
        target.Width             = source.Width;
        target.Height            = source.Height;
        target.SettingsConfigured = source.SettingsConfigured;
        target.Tracks            = source.Tracks.Select(t => t.Clone()).ToList();
    }
}
