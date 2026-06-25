using System;
using System.Collections.Immutable;
using Lumos.Domain;

namespace Lumos.Application.State;

/// Immutable undo/redo history. Stores timeline snapshots as a linear
/// stack with a pointer into the current position.
public sealed class HistoryState
{
    private readonly ImmutableList<HistoryEntry> _entries;
    private readonly int _currentIndex;

    public int Count      => _entries.Count;
    public int UndoCount  => _currentIndex;
    public int RedoCount  => _entries.Count - _currentIndex - 1;
    public bool CanUndo   => _currentIndex > 0;
    public bool CanRedo   => _currentIndex < _entries.Count - 1;

    public HistoryEntry? Current =>
        _currentIndex >= 0 && _currentIndex < _entries.Count
            ? _entries[_currentIndex]
            : null;

    private HistoryState(ImmutableList<HistoryEntry> entries, int currentIndex)
    {
        _entries      = entries;
        _currentIndex = currentIndex;
    }

    public static HistoryState Initial(Timeline timeline) =>
        new(ImmutableList.Create(new HistoryEntry("Initial", timeline.Clone(), DateTimeOffset.UtcNow)),
            0);

    /// Push a new entry, discarding any redo future.
    public HistoryState Push(string label, Timeline snapshot)
    {
        var truncated = _entries.GetRange(0, _currentIndex + 1);
        var next = truncated.Add(new HistoryEntry(label, snapshot.Clone(), DateTimeOffset.UtcNow));

        // Cap at 200 entries
        const int MaxEntries = 200;
        int newIndex = next.Count - 1;
        if (next.Count > MaxEntries)
        {
            next     = next.RemoveRange(0, next.Count - MaxEntries);
            newIndex = next.Count - 1;
        }

        return new HistoryState(next, newIndex);
    }

    /// Move one step backward and return the snapshot to restore.
    public (HistoryState State, Timeline Snapshot) Undo()
    {
        if (!CanUndo) throw new InvalidOperationException("Nothing to undo.");
        int target = _currentIndex - 1;
        return (new HistoryState(_entries, target), _entries[target].Snapshot.Clone());
    }

    /// Move one step forward and return the snapshot to restore.
    public (HistoryState State, Timeline Snapshot) Redo()
    {
        if (!CanRedo) throw new InvalidOperationException("Nothing to redo.");
        int target = _currentIndex + 1;
        return (new HistoryState(_entries, target), _entries[target].Snapshot.Clone());
    }

    /// Entries visible for UI display (recent first).
    public ImmutableList<HistoryEntry> GetEntries() => _entries;
}

public sealed record HistoryEntry(string Label, Timeline Snapshot, DateTimeOffset Timestamp);
