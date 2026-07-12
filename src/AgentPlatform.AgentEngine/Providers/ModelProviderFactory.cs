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
