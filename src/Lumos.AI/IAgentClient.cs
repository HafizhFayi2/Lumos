namespace Lumos.AI;

/// Streaming AI client protocol.
/// Each implementation handles a specific provider (Anthropic, etc.).
public interface IAgentClient
{
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string system,
        IReadOnlyList<AgentToolSchema> tools,
        IReadOnlyList<AgentMessage> messages,
        CancellationToken ct = default);
}
