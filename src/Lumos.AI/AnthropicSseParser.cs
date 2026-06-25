using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Lumos.AI;

/// Pure static SSE parser for Anthropic's messages API streaming format.
/// Extracted from AnthropicClient so it can be unit-tested without network calls.
public static class AnthropicSseParser
{
    /// Parse SSE lines from an async stream, yielding AgentStreamEvents.
    public static async IAsyncEnumerable<AgentStreamEvent> ParseAsync(
        IAsyncEnumerable<string> lines,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // pending tool accumulator: index → (id, name, partial json)
        var pendingTools = new Dictionary<int, (string Id, string Name, string Json)>();

        await foreach (var line in lines.WithCancellation(ct))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line["data:".Length..].TrimStart();
            if (payload == "[DONE]") yield break;

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                root = doc.RootElement.Clone();
            }
            catch (JsonException) { continue; }

            if (!root.TryGetProperty("type", out var typeProp)) continue;
            var type = typeProp.GetString();

            switch (type)
            {
                case "message_start":
                    // Log token usage in debug builds
#if DEBUG
                    if (root.TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("usage", out var usage))
                        LogUsage(usage);
#endif
                    break;

                case "content_block_start":
                    if (root.TryGetProperty("index", out var idxProp) &&
                        idxProp.TryGetInt32(out var idx) &&
                        root.TryGetProperty("content_block", out var block) &&
                        block.TryGetProperty("type", out var blockType) &&
                        blockType.GetString() == "tool_use" &&
                        block.TryGetProperty("id", out var idProp) &&
                        block.TryGetProperty("name", out var nameProp))
                    {
                        pendingTools[idx] = (idProp.GetString()!, nameProp.GetString()!, "");
                    }
                    break;

                case "content_block_delta":
                    if (!root.TryGetProperty("index", out var dIdx) || !dIdx.TryGetInt32(out var di)) break;
                    if (!root.TryGetProperty("delta", out var delta)) break;
                    if (!delta.TryGetProperty("type", out var dType)) break;

                    var deltaType = dType.GetString();
                    if (deltaType == "text_delta" &&
                        delta.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrEmpty(text))
                            yield return new AgentStreamEvent.TextDelta(text);
                    }
                    else if (deltaType == "input_json_delta" &&
                             delta.TryGetProperty("partial_json", out var partialProp) &&
                             pendingTools.TryGetValue(di, out var acc))
                    {
                        pendingTools[di] = acc with { Json = acc.Json + partialProp.GetString() };
                    }
                    break;

                case "content_block_stop":
                    if (root.TryGetProperty("index", out var stopIdx) &&
                        stopIdx.TryGetInt32(out var si) &&
                        pendingTools.Remove(si, out var finished))
                    {
                        var json = string.IsNullOrEmpty(finished.Json) ? "{}" : finished.Json;
                        yield return new AgentStreamEvent.ToolUseComplete(finished.Id, finished.Name, json);
                    }
                    break;

                case "message_delta":
                    if (root.TryGetProperty("delta", out var msgDelta) &&
                        msgDelta.TryGetProperty("stop_reason", out var sr))
                    {
                        yield return new AgentStreamEvent.MessageStop(ParseStopReason(sr.GetString()));
                    }
                    break;

                case "error":
                    if (root.TryGetProperty("error", out var err) &&
                        err.TryGetProperty("message", out var errMsg))
                        throw new AnthropicException(errMsg.GetString() ?? "Unknown streaming error");
                    break;
            }
        }
    }

    private static StopReason ParseStopReason(string? raw) => raw switch
    {
        "end_turn"       => StopReason.EndTurn,
        "tool_use"       => StopReason.ToolUse,
        "max_tokens"     => StopReason.MaxTokens,
        "stop_sequence"  => StopReason.StopSequence,
        _                => StopReason.Other,
    };

#if DEBUG
    private static void LogUsage(JsonElement usage)
    {
        var input      = usage.TryGetProperty("input_tokens",                 out var i)   ? i.GetInt32()   : 0;
        var cacheWrite = usage.TryGetProperty("cache_creation_input_tokens",  out var cw)  ? cw.GetInt32()  : 0;
        var cacheRead  = usage.TryGetProperty("cache_read_input_tokens",      out var cr)  ? cr.GetInt32()  : 0;
        var billed     = input + cacheWrite + cacheRead;
        var pct        = billed > 0 ? (int)((double)cacheRead / billed * 100) : 0;
        System.Diagnostics.Debug.WriteLine(
            $"[agent cache] input={input} cacheWrite={cacheWrite} cacheRead={cacheRead} ({pct}% read)");
    }
#endif
}

public sealed class AnthropicException(string message) : Exception(message);
