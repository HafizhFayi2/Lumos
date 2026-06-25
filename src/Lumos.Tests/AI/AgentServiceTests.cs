using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Lumos.AI;

namespace Lumos.Tests.AI;

public class AgentServiceTests
{
    private sealed class FakeAgentClient : IAgentClient
    {
        public int StreamCallsCount { get; private set; }

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            string system,
            IReadOnlyList<AgentToolSchema> tools,
            IReadOnlyList<AgentMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            StreamCallsCount++;
            if (StreamCallsCount == 1)
            {
                yield return new AgentStreamEvent.TextDelta("Calling tool... ");
                yield return new AgentStreamEvent.ToolUseComplete("call_1", "test_tool", "{\"arg\":123}");
                yield return new AgentStreamEvent.MessageStop(StopReason.ToolUse);
            }
            else
            {
                yield return new AgentStreamEvent.TextDelta("Done!");
                yield return new AgentStreamEvent.MessageStop(StopReason.EndTurn);
            }
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunTurnAsync_ExecutesToolUseLoop_AndYieldsText()
    {
        var client = new FakeAgentClient();
        var dispatchedCalls = new List<(string Name, string Json)>();

        Func<string, JsonElement, CancellationToken, Task<string>> dispatcher = (name, args, ct) =>
        {
            dispatchedCalls.Add((name, args.ToString()));
            return Task.FromResult("{\"status\":\"success\"}");
        };

        var service = new AgentService(() => client, dispatcher, "system prompt");
        var history = new List<AgentMessage>();
        var tools = new List<AgentToolSchema> { new AgentToolSchema("test_tool", "desc", new object()) };

        var textChunks = new List<string>();
        await foreach (var text in service.RunTurnAsync(history, "Hello", tools))
        {
            textChunks.Add(text);
        }

        // Verify we got the text from both turns
        Assert.Equal(2, textChunks.Count);
        Assert.Equal("Calling tool... ", textChunks[0]);
        Assert.Equal("Done!", textChunks[1]);

        // Verify the tool dispatcher was called
        Assert.Single(dispatchedCalls);
        Assert.Equal("test_tool", dispatchedCalls[0].Name);
        Assert.Contains("123", dispatchedCalls[0].Json);

        // Verify the client was called twice
        Assert.Equal(2, client.StreamCallsCount);
    }
}
