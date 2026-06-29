namespace Lumos.Application;

/// A single agent action recorded for audit and undo support.
public sealed record AgentAction(
    string Id,
    string ToolName,
    string Description,
    DateTime Timestamp,
    bool Succeeded,
    string? Details = null);

/// Thread-safe ring buffer that tracks the last N agent actions.
/// Used for agent verification, undo, and the "Agent Action History" panel.
public sealed class AgentActionHistory
{
    private readonly List<AgentAction> _actions = new();
    private readonly int _maxCapacity;
    private readonly object _lock = new();

    public event EventHandler<AgentAction>? ActionAdded;

    public AgentActionHistory(int maxCapacity = 100)
    {
        _maxCapacity = maxCapacity;
    }

    public void Record(string toolName, string description, bool succeeded, string? details = null)
    {
        var action = new AgentAction(
            Guid.NewGuid().ToString("N")[..8],
            toolName,
            description,
            DateTime.UtcNow,
            succeeded,
            details);

        lock (_lock)
        {
            _actions.Add(action);
            if (_actions.Count > _maxCapacity)
                _actions.RemoveAt(0);
        }

        ActionAdded?.Invoke(this, action);
    }

    public IReadOnlyList<AgentAction> GetRecent(int count = 20)
    {
        lock (_lock)
        {
            int start = Math.Max(0, _actions.Count - count);
            return _actions.Skip(start).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<AgentAction> GetAll()
    {
        lock (_lock)
        {
            return _actions.ToList().AsReadOnly();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _actions.Clear();
        }
    }
}
