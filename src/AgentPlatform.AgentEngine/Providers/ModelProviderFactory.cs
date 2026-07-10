using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.AgentEngine.Providers;

/// <summary>
/// 模型提供器工厂：将 IModelProvider 桥接到 MAF 的 IChatClient。
///
/// MAF 阶段：通过 IChatClient 对接 ChatClientAgent。
/// 当前保留 IModelProvider 作为底层实现，用适配器模式桥接到 IChatClient。
/// </summary>
public class ModelProviderFactory
{
    private readonly IModelProvider _defaultProvider;
    private readonly ILogger<ModelProviderFactory> _logger;

    public ModelProviderFactory(IModelProvider defaultProvider, ILogger<ModelProviderFactory> logger)
    {
        _defaultProvider = defaultProvider;
        _logger = logger;
    }

    /// <summary>
    /// 根据 Agent 实体创建 IChatClient（MAF 标准接口）。
    /// 使用适配器将 IModelProvider 包装为 IChatClient。
    /// </summary>
    public Task<IChatClient> CreateChatClientAsync(Agent agent, ModelEndpoint? endpoint = null)
    {
        _logger.LogInformation(
            "Creating MAF IChatClient for agent {AgentName} (model: {ModelId})",
            agent.Name, agent.ModelId);

        // 使用适配器桥接 IModelProvider → IChatClient
        var chatClient = new ModelProviderChatClientAdapter(_defaultProvider, agent.ModelId);
        return Task.FromResult<IChatClient>(chatClient);
    }

    /// <summary>
    /// 根据 Agent 实体创建对应的 IModelProvider（兼容旧接口，内部过渡用）。
    /// </summary>
    public Task<IModelProvider> CreateProviderAsync(Agent agent, ModelEndpoint? endpoint = null)
    {
        _logger.LogInformation(
            "Creating legacy model provider for agent {AgentName} (model: {ModelId})",
            agent.Name, agent.ModelId);

        return Task.FromResult(_defaultProvider);
    }

    /// <summary>
    /// 构建 MAF ChatOptions（使用 Microsoft.Extensions.AI.ChatOptions）
    /// </summary>
    public Microsoft.Extensions.AI.ChatOptions? BuildChatOptions(Agent agent)
    {
        if (agent.Temperature is null && agent.MaxTokens is null && agent.TopP is null && string.IsNullOrEmpty(agent.SystemPrompt))
            return null;

        return new Microsoft.Extensions.AI.ChatOptions
        {
            Temperature = agent.Temperature.HasValue ? (float)agent.Temperature.Value : null,
            MaxOutputTokens = agent.MaxTokens,
            TopP = agent.TopP.HasValue ? (float)agent.TopP.Value : null,
            Instructions = agent.SystemPrompt,
        };
    }
}

/// <summary>
/// IChatClient 适配器：将现有的 IModelProvider 包装为 MAF 标准的 IChatClient。
/// 实现 IChatClient 接口，内部委托给 IModelProvider。
/// </summary>
public class ModelProviderChatClientAdapter : IChatClient
{
    private readonly IModelProvider _provider;
    private readonly string _modelId;

    public ModelProviderChatClientAdapter(IModelProvider provider, string modelId)
    {
        _provider = provider;
        _modelId = modelId;
    }

    /// <inheritdoc/>
    public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        Microsoft.Extensions.AI.ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest
        {
            ModelId = options?.ModelId ?? _modelId,
            Messages = messages.Select(m => new AgentPlatform.Core.Interfaces.ChatMessage
            {
                Role = m.Role.Value?.ToLower() ?? "user",
                Content = m.Text ?? string.Empty
            }).ToList(),
            MaxTokens = options?.MaxOutputTokens ?? 4096,
            Temperature = options?.Temperature ?? 0.7f,
            TopP = options?.TopP
        };

        var response = await _provider.CompleteAsync(request, cancellationToken);

        return new Microsoft.Extensions.AI.ChatResponse(new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, response.Content))
        {
            ResponseId = response.Id,
            ModelId = response.Model,
            Usage = response.InputTokens > 0 || response.OutputTokens > 0
                ? new UsageDetails
                {
                    InputTokenCount = response.InputTokens,
                    OutputTokenCount = response.OutputTokens
                }
                : null
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Microsoft.Extensions.AI.ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        Microsoft.Extensions.AI.ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatCompletionRequest
        {
            ModelId = options?.ModelId ?? _modelId,
            Messages = messages.Select(m => new AgentPlatform.Core.Interfaces.ChatMessage
            {
                Role = m.Role.Value?.ToLower() ?? "user",
                Content = m.Text ?? string.Empty
            }).ToList(),
            MaxTokens = options?.MaxOutputTokens ?? 4096,
            Temperature = options?.Temperature ?? 0.7f,
            TopP = options?.TopP
        };

        await foreach (var chunk in _provider.CompleteStreamAsync(request, cancellationToken))
        {
            yield return new Microsoft.Extensions.AI.ChatResponseUpdate(
                Microsoft.Extensions.AI.ChatRole.Assistant, chunk);
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IModelProvider))
            return _provider;
        return null;
    }

    void IDisposable.Dispose() { }
}
