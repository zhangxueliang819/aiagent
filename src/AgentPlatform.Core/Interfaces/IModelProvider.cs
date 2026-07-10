namespace AgentPlatform.Core.Interfaces;

public interface IModelProvider
{
    string ProviderName { get; }
    Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken ct);
    IAsyncEnumerable<string> CompleteStreamAsync(ChatCompletionRequest request, CancellationToken ct);
    Task<bool> HealthCheckAsync();
}

public class ChatCompletionRequest
{
    public string ModelId { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; set; } = new();
    public int MaxTokens { get; set; } = 4096;
    public float Temperature { get; set; } = 0.7f;
    public float? TopP { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    /// <summary>工具调用 ID：用于关联 assistant 的 tool_calls 与 function/tool 的响应</summary>
    public string? ToolCallId { get; set; }
    /// <summary>工具调用信息列表：assistant 发起 function call 时使用</summary>
    public List<ToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// 工具调用描述：对应 OpenAI tool_calls 数组元素
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
}

public class ChatCompletionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>模型的推理/思考过程（如 DeepSeek-R1 的 reasoning_content）</summary>
    public string? Thinking { get; set; }
    /// <summary>LLM 返回的原始 JSON 响应</summary>
    public string? RawResponse { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
