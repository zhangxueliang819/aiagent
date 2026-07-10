using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.ModelProviders.OpenAI;

/// <summary>
/// OpenAI 兼容 API 的 IChatClient 实现（V2.0 MEAI 标准接口）。
/// 支持 OpenAI、Azure OpenAI 以及任何兼容 /v1/chat/completions 的 API。
/// </summary>
public class OpenAIProvider : IChatClient
{
    private readonly string _apiBaseUrl;
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public string ProviderName => "OpenAI";

    public OpenAIProvider(
        string apiBaseUrl,
        string apiKey,
        string modelId,
        HttpClient httpClient,
        ILogger<OpenAIProvider> logger)
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _modelId = modelId;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model = options?.ModelId ?? _modelId,
            messages = BuildMessageObjects(messages),
            max_tokens = options?.MaxOutputTokens ?? 4096,
            temperature = options?.Temperature ?? 0.7f,
            top_p = options?.TopP ?? 1,
            stream = false
        };

        var json = await SendRequestAsync(body, cancellationToken);
        return ParseChatResponse(json, options?.ModelId ?? _modelId);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model = options?.ModelId ?? _modelId,
            messages = BuildMessageObjects(messages),
            max_tokens = options?.MaxOutputTokens ?? 4096,
            temperature = options?.Temperature ?? 0.7f,
            top_p = options?.TopP ?? 1,
            stream = true,
            stream_options = new { include_usage = true }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI API error ({Status}): {Body}", (int)response.StatusCode, errBody);
            throw new HttpRequestException($"OpenAI API error ({response.StatusCode}): {errBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null && !cancellationToken.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            var (content, reasoning) = TryExtractDelta(data);
            if (content is not null || reasoning is not null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, content)
                {
                    AdditionalProperties = reasoning is not null
                        ? new AdditionalPropertiesDictionary { ["reasoning_content"] = reasoning }
                        : null
                };
            }
        }
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var msg = new ChatMessage(ChatRole.User, "ping");
            await GetResponseAsync([msg], cancellationToken: cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI health check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    void IDisposable.Dispose() { }

    // ─── Private Helpers ────────────────────────────────────────────

    private async Task<string> SendRequestAsync(object body, CancellationToken ct)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.SendAsync(httpRequest, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error ({Status}): {Body}", (int)response.StatusCode, responseBody);
            throw new HttpRequestException($"OpenAI API error ({response.StatusCode}): {responseBody}");
        }

        return responseBody;
    }

    private ChatResponse ParseChatResponse(string json, string modelId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        var model = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() ?? modelId : modelId;

        var choices = root.GetProperty("choices");
        var firstChoice = choices[0];
        var message = firstChoice.GetProperty("message");
        var content = message.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "";

        // 解析推理/思考内容（DeepSeek-R1 等模型支持）
        string? thinking = null;
        if (message.TryGetProperty("reasoning_content", out var reasoningProp))
            thinking = reasoningProp.GetString();

        int inputTokens = 0, outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var p))
                inputTokens = p.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var c))
                outputTokens = c.GetInt32();
        }

        var responseMsg = new ChatMessage(ChatRole.Assistant, content);
        var additionalProps = new AdditionalPropertiesDictionary();
        if (thinking is not null)
            additionalProps["thinking"] = thinking;
        if (!string.IsNullOrEmpty(id))
            additionalProps["response_id"] = id;

        return new ChatResponse(responseMsg)
        {
            ResponseId = id,
            ModelId = model,
            AdditionalProperties = additionalProps.Count > 0 ? additionalProps : null,
            Usage = inputTokens > 0 || outputTokens > 0
                ? new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens
                }
                : null
        };
    }

    /// <summary>
    /// 将 MEAI ChatMessage 序列化为 OpenAI API 兼容的消息对象。
    /// 处理 Contents 中的 FunctionCallContent / FunctionResultContent。
    /// </summary>
    private static IEnumerable<object> BuildMessageObjects(IEnumerable<ChatMessage> messages)
    {
        foreach (var m in messages)
        {
            var role = MapRole(m.Role);

            // 检查 Contents 中是否有 FunctionCallContent (assistant tool_calls)
            var functionCalls = m.Contents.OfType<FunctionCallContent>().ToList();
            if (functionCalls.Count > 0)
            {
                var msg = new Dictionary<string, object?>
                {
                    ["role"] = role,
                    ["content"] = null,
                    ["tool_calls"] = functionCalls.Select(fc => new Dictionary<string, object?>
                    {
                        ["id"] = fc.CallId,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object?>
                        {
                            ["name"] = fc.Name,
                            ["arguments"] = fc.Arguments is IDictionary<string, object?> dict
                                ? JsonSerializer.Serialize(dict)
                                : JsonSerializer.Serialize(fc.Arguments)
                        }
                    }).ToList()
                };
                if (!string.IsNullOrWhiteSpace(m.Text))
                    msg["content"] = m.Text;
                yield return msg;
                continue;
            }

            // 检查 Contents 中是否有 FunctionResultContent (tool result)
            var functionResults = m.Contents.OfType<FunctionResultContent>().ToList();
            if (functionResults.Count > 0)
            {
                foreach (var fr in functionResults)
                {
                    var toolMsg = new Dictionary<string, object?>
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = fr.CallId,
                        ["content"] = fr.Result?.ToString() ?? ""
                    };
                    yield return toolMsg;
                }
                // Also yield the original text if there's additional content
                if (!string.IsNullOrWhiteSpace(m.Text) && functionResults.Count > 0)
                    continue; // tool messages replace the parent message
                continue;
            }

            // 普通文本消息
            yield return new Dictionary<string, object?>
            {
                ["role"] = role,
                ["content"] = m.Text ?? string.Empty
            };
        }
    }

    private static string MapRole(ChatRole role) => role.Value?.ToLower() switch
    {
        "system" => "system",
        "user" => "user",
        "assistant" => "assistant",
        "tool" => "tool",
        "function" => "tool",
        _ => "user"
    };

    private static (string? Content, string? ReasoningContent) TryExtractDelta(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            if (root.TryGetProperty("usage", out _)) return (null, null);

            if (!root.TryGetProperty("choices", out var choices)) return (null, null);
            if (choices.GetArrayLength() == 0) return (null, null);

            var first = choices[0];
            if (!first.TryGetProperty("delta", out var delta)) return (null, null);

            string? content = null;
            if (delta.TryGetProperty("content", out var contentProp))
                content = contentProp.GetString();

            string? reasoning = null;
            if (delta.TryGetProperty("reasoning_content", out var reasoningProp))
                reasoning = reasoningProp.GetString();

            return (content, reasoning);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
