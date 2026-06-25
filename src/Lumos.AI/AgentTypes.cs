namespace Lumos.AI;

/// A message in a multi-turn conversation.
public sealed record AgentMessage(AgentRole Role, IReadOnlyList<MessageBlock> Content);

public enum AgentRole { User, Assistant }

/// A block within a message — text or tool result.
public abstract record MessageBlock
{
    public sealed record Text(string Value) : MessageBlock;
    public sealed record ToolResult(string ToolUseId, string Content) : MessageBlock;
    public sealed record ToolUse(string Id, string Name, string InputJson) : MessageBlock;
}

/// Tool schema sent to the model for tool-use.
public sealed record AgentToolSchema(string Name, string Description, object InputSchema);
