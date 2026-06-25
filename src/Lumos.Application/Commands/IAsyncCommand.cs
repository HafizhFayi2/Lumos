using System.Threading.Tasks;

namespace Lumos.Application.Commands;

/// Async command interface. All timeline operations implement this.
public interface IAsyncCommand
{
    /// Human-readable label for undo history and status bar.
    string Label { get; }

    /// Execute the command. The context carries the EditorStore, cancellation, and progress.
    Task<CommandResult> ExecuteAsync(CommandContext context);

    /// Reverse the command's effects. Called by undo.
    Task<CommandResult> UndoAsync(CommandContext context);

    /// Whether this command can be merged with the previous command of the same type.
    /// Used for coalescing rapid-fire mutations (e.g., scrub, volume drag).
    bool CanMergeWith(IAsyncCommand previous) => false;

    /// Merge this command's intent into the previous command. Only called when CanMergeWith returns true.
    IAsyncCommand MergeWith(IAsyncCommand previous) => this;
}
