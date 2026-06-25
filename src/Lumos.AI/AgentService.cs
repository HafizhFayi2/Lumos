using System.Text.Json;

namespace Lumos.AI;

/// Orchestrates a multi-turn AI conversation with tool-use loop.
/// Mirrors upstream AgentService.swift — sends user message, streams response,
/// executes tool calls via the provided dispatcher, then continues until end_turn.
public sealed class AgentService
{
    private readonly Func<IAgentClient?> _clientFactory;
    private readonly Func<string, JsonElement, CancellationToken, Task<string>> _toolDispatcher;
    private readonly string _systemPrompt;

    public AnthropicModel Model { get; set; } = AnthropicModel.Sonnet46;
    public bool IsStreaming { get; private set; }

    public AgentService(
        Func<IAgentClient?> clientFactory,
        Func<string, JsonElement, CancellationToken, Task<string>> toolDispatcher,
        string systemPrompt)
    {
        _clientFactory  = clientFactory;
        _toolDispatcher = toolDispatcher;
        _systemPrompt   = systemPrompt;
    }

    /// Run a complete agent turn: sends messages, streams the response,
    /// executes any tool calls, then continues until end_turn or max depth.
    /// Yields text deltas as they arrive.
    public async IAsyncEnumerable<string> RunTurnAsync(
        IReadOnlyList<AgentMessage> history,
        string userMessage,
        IReadOnlyList<AgentToolSchema> tools,
        System.Runtime.CompilerServices.EnumeratorCancellationAttribute _ = default!,
        CancellationToken ct = default)
    {
        var client = _clientFactory()
            ?? throw new InvalidOperationException("No AI client available. Set an API key.");

        // Build the full message list for this turn
        var messages = new List<AgentMessage>(history)
        {
            new AgentMessage(AgentRole.User, new[] { new MessageBlock.Text(userMessage) }),
        };

        IsStreaming = true;
        try
        {
            const int maxDepth = 10;
            for (int depth = 0; depth < maxDepth; depth++)
            {
                var toolCalls = new List<AgentStreamEvent.ToolUseComplete>();
                var assistantBlocks = new List<MessageBlock>();
                var textBuffer = new System.Text.StringBuilder();

                await foreach (var evt in client.StreamAsync(_systemPrompt, tools, messages, ct))
                {
                    switch (evt)
                    {
                        case AgentStreamEvent.TextDelta td:
                            textBuffer.Append(td.Text);
                            yield return td.Text;
                            break;

                        case AgentStreamEvent.ToolUseComplete tu:
                            toolCalls.Add(tu);
                            break;

                        case AgentStreamEvent.MessageStop ms when ms.Reason != StopReason.ToolUse:
                            // Not a tool-use stop — we're done
                            if (textBuffer.Length > 0)
                                assistantBlocks.Add(new MessageBlock.Text(textBuffer.ToString()));
                            goto done;
                    }
                }

                // Flush text into assistant turn
                if (textBuffer.Length > 0)
                    assistantBlocks.Add(new MessageBlock.Text(textBuffer.ToString()));

                // Append tool-use blocks
                foreach (var tc in toolCalls)
                    assistantBlocks.Add(new MessageBlock.ToolUse(tc.Id, tc.Name, tc.InputJson));

                // Record the assistant turn
                messages.Add(new AgentMessage(AgentRole.Assistant, assistantBlocks));

                if (toolCalls.Count == 0) break;

                // Execute each tool call and build the user-side tool results
                var resultBlocks = new List<MessageBlock>();
                foreach (var tc in toolCalls)
                {
                    JsonElement argsEl;
                    try
                    {
                        using var doc = JsonDocument.Parse(tc.InputJson);
                        argsEl = doc.RootElement.Clone();
                    }
                    catch
                    {
                        argsEl = JsonDocument.Parse("{}").RootElement;
                    }

                    var resultJson = await _toolDispatcher(tc.Name, argsEl, ct);
                    resultBlocks.Add(new MessageBlock.ToolResult(tc.Id, resultJson));
                }

                messages.Add(new AgentMessage(AgentRole.User, resultBlocks));
            }

            done:;
        }
        finally
        {
            IsStreaming = false;
        }
    }
}
