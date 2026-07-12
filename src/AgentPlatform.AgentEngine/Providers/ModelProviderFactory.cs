using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine.Providers;

/// <summary>
/// 模型提供器工厂（V2.0）：构建 MAF ChatOptions。
/// IChatClient 的解析由 ModelRouter 通过 ModelEndpointId 完成。
/// </summary>
public class ModelProviderFactory
{
    private readonly ILogger<ModelProviderFactory> _logger;

    public ModelProviderFactory(ILogger<ModelProviderFactory> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 此方法已弃用 — IChatClient 的解析应通过 ModelRouter 完成。
    /// </summary>
    [Obsolete("Use ModelRouter to resolve IChatClient per agent.")]
    public Task<IChatClient> CreateChatClientAsync(Agent agent, ModelEndpoint? endpoint = null)
    {
        throw new NotSupportedException(
            "IChatClient resolution via ModelProviderFactory is no longer supported. " +
            "Use CompleteAgentFactory or ModelRouter to resolve per-agent clients.");
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
