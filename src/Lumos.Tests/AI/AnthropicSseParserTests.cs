using Lumos.AI;
using Xunit;

namespace Lumos.Tests.AI;

public sealed class AnthropicSseParserTests
{
    private static async IAsyncEnumerable<string> Lines(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }

    private static async Task<List<AgentStreamEvent>> Collect(IAsyncEnumerable<string> lines)
    {
        var events = new List<AgentStreamEvent>();
        await foreach (var evt in AnthropicSseParser.ParseAsync(lines))
            events.Add(evt);
        return events;
    }

    [Fact]
    public async Task TextDelta_EmittedForTextDeltaEvents()
    {
        var raw = Lines(
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\" world\"}}"
        );

        var events = await Collect(raw);

        Assert.Equal(2, events.Count);
        Assert.IsType<AgentStreamEvent.TextDelta>(events[0]);
        Assert.Equal("Hello",  ((AgentStreamEvent.TextDelta)events[0]).Text);
        Assert.Equal(" world", ((AgentStreamEvent.TextDelta)events[1]).Text);
    }

    [Fact]
    public async Task ToolUseComplete_EmittedWhenBlockStops()
    {
        var raw = Lines(
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\",\"id\":\"tu_123\",\"name\":\"split_clip\"}}",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"clip_id\\\":\\\"abc\\\"\"}}",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\",\\\"split_frame\\\":30}\"}}",
            "data: {\"type\":\"content_block_stop\",\"index\":0}"
        );

        var events = await Collect(raw);

        Assert.Single(events);
        var tu = Assert.IsType<AgentStreamEvent.ToolUseComplete>(events[0]);
        Assert.Equal("tu_123",    tu.Id);
        Assert.Equal("split_clip", tu.Name);
        Assert.Contains("clip_id", tu.InputJson);
    }

    [Fact]
    public async Task MessageStop_EmittedWithCorrectReason()
    {
        var raw = Lines(
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null}}"
        );

        var events = await Collect(raw);

        Assert.Single(events);
        var stop = Assert.IsType<AgentStreamEvent.MessageStop>(events[0]);
        Assert.Equal(StopReason.EndTurn, stop.Reason);
    }

    [Fact]
    public async Task ToolUseStop_ParsedAsToolUseReason()
    {
        var raw = Lines(
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"tool_use\"}}"
        );

        var events = await Collect(raw);
        var stop = Assert.IsType<AgentStreamEvent.MessageStop>(events[0]);
        Assert.Equal(StopReason.ToolUse, stop.Reason);
    }

    [Fact]
    public async Task NonDataLines_AreIgnored()
    {
        var raw = Lines(
            "event: message_start",
            "",
            "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}"
        );

        var events = await Collect(raw);

        // Only the data line should yield an event
        Assert.Single(events);
    }

    [Fact]
    public async Task EmptyTextDelta_IsNotEmitted()
    {
        var raw = Lines(
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"\"}}"
        );

        var events = await Collect(raw);
        Assert.Empty(events);
    }

    [Fact]
    public async Task StreamError_ThrowsAnthropicException()
    {
        var raw = Lines(
            "data: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}"
        );

        await Assert.ThrowsAsync<AnthropicException>(
            async () => await Collect(raw));
    }

    [Fact]
    public async Task MultipleToolCalls_EachEmittedSeparately()
    {
        var raw = Lines(
            "data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"tool_a\"}}",
            "data: {\"type\":\"content_block_stop\",\"index\":0}",
            "data: {\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"tool_use\",\"id\":\"t2\",\"name\":\"tool_b\"}}",
            "data: {\"type\":\"content_block_stop\",\"index\":1}"
        );

        var events = await Collect(raw);

        Assert.Equal(2, events.Count);
        Assert.Equal("t1", ((AgentStreamEvent.ToolUseComplete)events[0]).Id);
        Assert.Equal("t2", ((AgentStreamEvent.ToolUseComplete)events[1]).Id);
    }
}
