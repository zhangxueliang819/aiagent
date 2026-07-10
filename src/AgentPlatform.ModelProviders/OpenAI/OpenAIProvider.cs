using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.ModelProviders.OpenAI;

/// <summary>
/// OpenAI 兼容 API 的 LLM Provider 实现。
/// 支持 OpenAI、Azure OpenAI 以及任何兼容 /v1/chat/completions 的 API。
/// 
/// 功能：
/// - 非流式对话 (CompleteAsync)
/// - SSE 流式对话 (CompleteStreamAsync)
/// - 自动解析 usage tokens
/// </summary>
public class OpenAIProvider : IModelProvider
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
    public async Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        var body = new
        {
            model = _modelId,
            messages = BuildMessageObjects(request.Messages),
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            top_p = request.TopP ?? 1,
            stream = false
        };

        var json = await SendRequestAsync(body, ct);
        return ParseCompletionResponse(json);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> CompleteStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var body = new
        {
            model = _modelId,
            messages = BuildMessageObjects(request.Messages),
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            top_p = request.TopP ?? 1,
            stream = true,
            stream_options = new { include_usage = true }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_apiBaseUrl}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error ({Status}): {Body}", (int)response.StatusCode, errBody);
            throw new HttpRequestException($"OpenAI API error ({response.StatusCode}): {errBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            // Parse SSE data frame — skip malformed frames without throwing
            var text = TryExtractDeltaContent(data);
            if (text is not null)
                yield return text;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var request = new ChatCompletionRequest
            {
                Messages = new List<ChatMessage> { new() { Role = "user", Content = "ping" } },
                MaxTokens = 5,
                Temperature = 0f
            };
            await CompleteAsync(request, cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI health check failed");
            return false;
        }
    }

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

    private ChatCompletionResponse ParseCompletionResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        var model = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() ?? _modelId : _modelId;

        var choices = root.GetProperty("choices");
        var firstChoice = choices[0];
        var message = firstChoice.GetProperty("message");
        var content = message.GetProperty("content").GetString() ?? "";

        // 解析推理/思考内容（DeepSeek-R1 等模型支持）
        string? thinking = null;
        if (message.TryGetProperty("reasoning_content", out var reasoningProp))
        {
            thinking = reasoningProp.GetString();
        }

        int inputTokens = 0, outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var p))
                inputTokens = p.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var c))
                outputTokens = c.GetInt32();
        }

        return new ChatCompletionResponse
        {
            Id = id,
            Model = model,
            Content = content,
            Thinking = thinking,
            RawResponse = json,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    /// <summary>
    /// 映射角色名到 OpenAI 期望格式（system / user / assistant / function / tool）
    /// </summary>
    private static string MapRole(string role) => role.ToLower() switch
    {
        "system" => "system",
        "user" => "user",
        "assistant" => "assistant",
        "function" => "tool",   // OpenAI 现在用 tool 角色
        "tool" => "tool",
        _ => "user"
    };

    /// <summary>
    /// 构建符合 OpenAI API 规范的消息对象列表（支持 tool_calls 和 tool_call_id）
    /// </summary>
    private static IEnumerable<object> BuildMessageObjects(List<ChatMessage> messages)
    {
        foreach (var m in messages)
        {
            var msg = new Dictionary<string, object?> { ["role"] = MapRole(m.Role) };

            if (m.ToolCalls is { Count: > 0 })
            {
                // Assistant 发起 function call——使用 tool_calls 数组，content 设为 null
                msg["content"] = null;
                msg["tool_calls"] = m.ToolCalls.Select(tc => new Dictionary<string, object?>
                {
                    ["id"] = tc.Id,
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = tc.FunctionName,
                        ["arguments"] = tc.Arguments
                    }
                }).ToList();
            }
            else
            {
                msg["content"] = m.Content;
            }

            // Tool/function 响应消息需要 tool_call_id
            if (!string.IsNullOrEmpty(m.ToolCallId))
            {
                msg["tool_call_id"] = m.ToolCallId;
            }

            yield return msg;
        }
    }

    /// <summary>
    /// 从 SSE data 行提取 delta.content（流式响应的文本增量）
    /// 同时尝试提取 reasoning_content（推理过程）
    /// 返回 (content, reasoningContent) 元组
    /// </summary>
    private static (string? Content, string? ReasoningContent) TryExtractDelta(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            // usage chunk (final) — 跳过
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

    /// <summary>
    /// 从 SSE data 行提取 delta.content（流式响应的文本增量）
    /// 返回 null 表示跳过该帧（非 content 帧或解析失败）
    /// </summary>
    private static string? TryExtractDeltaContent(string data)
    {
        var (content, _) = TryExtractDelta(data);
        return string.IsNullOrEmpty(content) ? null : content;
    }
}
