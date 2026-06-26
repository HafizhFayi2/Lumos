using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lumos.AI;

/// Anthropic Claude streaming client.
/// Ported from upstream AnthropicClient.swift + AgentClientTypes.swift.
/// Uses System.Net.Http SSE — no third-party AI SDK required.
public sealed class AnthropicClient : IAgentClient
{
    private static readonly Uri Endpoint = new("https://api.anthropic.com/v1/messages");
    private static readonly HttpClient Http = new();

    private readonly string _apiKey;
    private readonly AnthropicModel _model;
    private readonly int _maxTokens;

    public AnthropicClient(string apiKey, AnthropicModel model, int maxTokens = 8192)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key must not be empty.", nameof(apiKey));
        _apiKey    = apiKey;
        _model     = model;
        _maxTokens = maxTokens;
    }

    /// Load API key from ANTHROPIC_API_KEY env var (debug) or the provided settings path.
    public static string? LoadApiKey() =>
#if DEBUG
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")?.Trim() is { Length: > 0 } env
            ? env
            : ReadFromSettingsFile();
#else
        ReadFromSettingsFile();
#endif

    public static void SaveApiKey(string key)
    {
        var datPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LumosDesktop", "anthropic_key.dat");
        
        Directory.CreateDirectory(Path.GetDirectoryName(datPath)!);
        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(datPath, encrypted);
        
        // Cleanup old txt if exists
        var txtPath = Path.Combine(Path.GetDirectoryName(datPath)!, "anthropic_key.txt");
        if (File.Exists(txtPath)) File.Delete(txtPath);
    }

    private static string? ReadFromSettingsFile()
    {
        var datPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LumosDesktop", "anthropic_key.dat");
            
        if (File.Exists(datPath))
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(datPath);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted).Trim();
            }
            catch
            {
                // Decryption failed (e.g. moved to different machine)
                return null;
            }
        }

        // Fallback to legacy plaintext for migration
        var txtPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LumosDesktop", "anthropic_key.txt");
            
        if (File.Exists(txtPath))
        {
            string key = File.ReadAllText(txtPath).Trim();
            if (!string.IsNullOrEmpty(key))
            {
                SaveApiKey(key); // Auto-migrate to encrypted
                return key;
            }
        }
        
        return null;
    }

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string system,
        IReadOnlyList<AgentToolSchema> tools,
        IReadOnlyList<AgentMessage> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body    = BuildRequestBody(system, tools, messages);
        var request = BuildRequest(body);

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new AnthropicException($"HTTP {(int)response.StatusCode}: {errorBody[..Math.Min(500, errorBody.Length)]}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var lines = ReadLinesAsync(reader, ct);
        await foreach (var evt in AnthropicSseParser.ParseAsync(lines, ct))
            yield return evt;
    }

    private HttpRequestMessage BuildRequest(object body)
    {
        var json    = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = false });
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key",        _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private object BuildRequestBody(
        string system,
        IReadOnlyList<AgentToolSchema> tools,
        IReadOnlyList<AgentMessage> messages)
    {
        // Build tool blocks with prompt-cache boundary on the last tool
        var toolBlocks = tools.Select((t, i) =>
        {
            var block = new Dictionary<string, object>
            {
                ["name"]         = t.Name,
                ["description"]  = t.Description,
                ["input_schema"] = t.InputSchema,
            };
            if (i == tools.Count - 1)
                block["cache_control"] = new { type = "ephemeral" };
            return block;
        }).ToList();

        // Build message blocks
        var messageBlocks = messages.Select((m, mi) =>
        {
            var contentBlocks = m.Content.Select((block, bi) =>
            {
                var b = BlockToDict(block);
                // Cache the last block of the last message
                if (mi == messages.Count - 1 && bi == m.Content.Count - 1)
                    b["cache_control"] = new { type = "ephemeral" };
                return b;
            }).ToList();
            return new Dictionary<string, object>
            {
                ["role"]    = m.Role == AgentRole.User ? "user" : "assistant",
                ["content"] = contentBlocks,
            };
        }).ToList();

        return new Dictionary<string, object>
        {
            ["model"]      = _model.ToApiString(),
            ["max_tokens"] = _maxTokens,
            ["stream"]     = true,
            ["system"] = new[] { new Dictionary<string, object>
            {
                ["type"] = "text", ["text"] = system,
                ["cache_control"] = new { type = "ephemeral" },
            }},
            ["tools"]    = toolBlocks,
            ["messages"] = messageBlocks,
        };
    }

    private static Dictionary<string, object> BlockToDict(MessageBlock block) => block switch
    {
        MessageBlock.Text t => new()
        {
            ["type"] = "text",
            ["text"] = t.Value,
        },
        MessageBlock.ToolResult r => new()
        {
            ["type"]        = "tool_result",
            ["tool_use_id"] = r.ToolUseId,
            ["content"]     = r.Content,
        },
        MessageBlock.ToolUse u => new()
        {
            ["type"]  = "tool_use",
            ["id"]    = u.Id,
            ["name"]  = u.Name,
            ["input"] = JsonDocument.Parse(u.InputJson).RootElement,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is not null) yield return line;
        }
    }
}
