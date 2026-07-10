using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.AgentEngine.Providers;

/// <summary>
/// 模型提供器工厂（V2.0）：直接创建 MEAI IChatClient，移除 IModelProvider 桥接。
/// 使用默认 IChatClient 作为回退，ModelEndpoint 配置的 Agent 可通过 ModelRouter 获取真实客户端。
/// </summary>
public class ModelProviderFactory
{
    private readonly IChatClient _defaultChatClient;
    private readonly ILogger<ModelProviderFactory> _logger;

    public ModelProviderFactory(IChatClient defaultChatClient, ILogger<ModelProviderFactory> logger)
    {
        _defaultChatClient = defaultChatClient;
        _logger = logger;
    }

    /// <summary>
    /// 根据 Agent 实体创建 IChatClient（MAF 标准接口）。
    /// V2.0: 直接返回默认 IChatClient（SimulatedModelProvider 或真实 OpenAI）。
    /// </summary>
    public Task<IChatClient> CreateChatClientAsync(Agent agent, ModelEndpoint? endpoint = null)
    {
        _logger.LogInformation(
            "Creating MAF IChatClient for agent {AgentName} (model: {ModelId})",
            agent.Name, agent.ModelId);

        return Task.FromResult(_defaultChatClient);
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
