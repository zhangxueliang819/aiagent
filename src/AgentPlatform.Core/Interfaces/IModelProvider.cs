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
}

public class ChatCompletionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
