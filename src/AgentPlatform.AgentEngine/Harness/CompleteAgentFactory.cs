using AgentPlatform.AgentEngine.Memory;
using AgentPlatform.AgentEngine.Middleware;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.Application.Services;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Harness;

/// <summary>
/// CompleteAgent 工厂 — 从 DI 容器中解析依赖并创建 CompleteAgent 实例。
///
/// 这是 CompleteAgent 与现有 DI 系统的集成入口：
/// 1. 使用 ModelRouter 按 Agent 配置解析 IChatClient
/// 2. 使用 CompleteAgentBuilder 进行完整配置
/// 3. 在需要时自动注入 FunctionTool / AgentSkill / MCP 工具
/// </summary>
public class CompleteAgentFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly FunctionToolRegistry _functionToolRegistry;
    private readonly UnifiedSkillProviderFactory _skillProviderFactory;
    private readonly McpSkillProvider _mcpSkillProvider;
    private readonly IAuditLogRepository _auditRepo;
    private readonly IChatClient _defaultChatClient;
    private readonly ModelRouter _modelRouter;

    public CompleteAgentFactory(
        ILoggerFactory loggerFactory,
        FunctionToolRegistry functionToolRegistry,
        UnifiedSkillProviderFactory skillProviderFactory,
        McpSkillProvider mcpSkillProvider,
        IAuditLogRepository auditRepo,
        IChatClient defaultChatClient,
        ModelRouter modelRouter)
    {
        _loggerFactory = loggerFactory;
        _functionToolRegistry = functionToolRegistry;
        _skillProviderFactory = skillProviderFactory;
        _mcpSkillProvider = mcpSkillProvider;
        _auditRepo = auditRepo;
        _defaultChatClient = defaultChatClient;
        _modelRouter = modelRouter;
    }

    /// <summary>
    /// 根据 Agent 配置解析 IChatClient。
    /// 优先通过 ModelRouter 按 Agent 的 ModelEndpointId 获取真实客户端，
    /// 回退到 DI 注入的默认客户端（SimulatedModelProvider）。
    /// </summary>
    private async Task<IChatClient> ResolveChatClientAsync(Agent entity, CancellationToken ct)
    {
        var realClient = await _modelRouter.ResolveAsync(entity, ct);
        if (realClient is not null)
            return realClient;
        return _defaultChatClient;
    }

    /// <summary>
    /// 创建一个具有完整能力的 CompleteAgent
    /// </summary>
    /// <param name="entity">Agent 实体</param>
    /// <param name="chatClient">IChatClient（可选，默认自动按 Agent 配置解析）</param>
    /// <param name="ct">取消令牌</param>
    public async Task<CompleteAgent> CreateAgentAsync(
        Agent entity,
        IChatClient? chatClient = null,
        CancellationToken ct = default)
    {
        var client = chatClient ?? await ResolveChatClientAsync(entity, ct);

        var builder = new CompleteAgentBuilder(_loggerFactory)
            .WithEntity(entity)
            .WithChatClient(client)
            .WithFunctionTools(_functionToolRegistry)
            .WithAgentSkills(_skillProviderFactory)
            .WithMcpTools(_mcpSkillProvider)
            // 默认中间件
            .WithLogging()
            .WithRateLimiting(60)
            .WithAudit(_auditRepo)
            // 默认上下文压缩（4K Token 预算，Summarize 模式）
            .WithContextCompression(
                maxTokens: 4096,
                strategy: CompressionStrategy.Summarize,
                summarizerClient: client);

        return await builder.BuildAsync(ct);
    }

    /// <summary>
    /// 创建一个最小配置的 CompleteAgent（仅基础功能）
    /// </summary>
    public async Task<CompleteAgent> CreateMinimalAgentAsync(
        Agent entity,
        IChatClient? chatClient = null,
        CancellationToken ct = default)
    {
        var client = chatClient ?? await ResolveChatClientAsync(entity, ct);

        var builder = new CompleteAgentBuilder(_loggerFactory)
            .WithEntity(entity)
            .WithChatClient(client)
            .WithLogging();

        return await builder.BuildAsync(ct);
    }

    /// <summary>
    /// 使用自定义配置创建 CompleteAgent
    /// </summary>
    public async Task<CompleteAgent> CreateCustomAgentAsync(
        Agent entity,
        Action<CompleteAgentBuilder> configure,
        IChatClient? chatClient = null,
        CancellationToken ct = default)
    {
        var client = chatClient ?? await ResolveChatClientAsync(entity, ct);

        var builder = new CompleteAgentBuilder(_loggerFactory)
            .WithEntity(entity)
            .WithChatClient(client);

        configure(builder);

        return await builder.BuildAsync(ct);
    }
}
