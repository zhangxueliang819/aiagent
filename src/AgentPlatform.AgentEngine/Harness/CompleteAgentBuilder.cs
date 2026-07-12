using AgentPlatform.AgentEngine.Middleware;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.Core.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Harness;

/// <summary>
/// CompleteAgent 构建器 — 流式 API，提供完整的 Agent 配置能力。
///
/// 用法：
/// <code>
/// var agent = new CompleteAgentBuilder(loggerFactory)
///     .WithEntity(entity)
///     .WithChatClient(chatClient)
///     .WithFunctionTools(functionToolRegistry)
///     .WithAgentSkills(skillProviderFactory)
///     .WithMiddleware(new LoggingMiddleware(logger))
///     .Build();
/// </code>
///
/// 设计依据（Microsoft Agent Framework 文档 — 步骤 1~4）：
/// - 步骤 1：创建代理并获取响应
/// - 步骤 2：添加工具
/// - 步骤 3：多轮次对话
/// - 步骤 4：内存和持久性（通过 AIContextProvider）
/// </summary>
public class CompleteAgentBuilder
{
    private readonly ILoggerFactory _loggerFactory;
    private Agent? _entity;
    private IChatClient? _chatClient;
    private readonly List<IAgentMiddleware> _middlewares = new();
    private FunctionToolRegistry? _functionToolRegistry;
    private UnifiedSkillProviderFactory? _skillProviderFactory;
    private McpSkillProvider? _mcpSkillProvider;
    private Microsoft.Agents.AI.ChatClientAgentOptions? _agentOptions;

    public CompleteAgentBuilder(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    // ══════════════════════════════════════════════════════════════
    //  基础配置
    // ══════════════════════════════════════════════════════════════

    /// <summary>设置 Agent 实体（数据模型）</summary>
    public CompleteAgentBuilder WithEntity(Agent entity)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        return this;
    }

    /// <summary>设置 IChatClient（LLM 推理客户端）</summary>
    public CompleteAgentBuilder WithChatClient(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        return this;
    }

    /// <summary>设置 ChatClientAgentOptions（MAF 选项）</summary>
    public CompleteAgentBuilder WithAgentOptions(Microsoft.Agents.AI.ChatClientAgentOptions options)
    {
        _agentOptions = options;
        return this;
    }

    // ══════════════════════════════════════════════════════════════
    //  中间件
    // ══════════════════════════════════════════════════════════════

    /// <summary>添加日志中间件（默认行为）</summary>
    public CompleteAgentBuilder WithLogging()
    {
        _middlewares.Add(new LoggingMiddleware(
            _loggerFactory.CreateLogger<LoggingMiddleware>()));
        return this;
    }

    /// <summary>添加速率限制中间件</summary>
    public CompleteAgentBuilder WithRateLimiting(int maxRequestsPerMinute = 60)
    {
        _middlewares.Add(new RateLimitingMiddleware(
            _loggerFactory.CreateLogger<RateLimitingMiddleware>(),
            maxRequestsPerMinute));
        return this;
    }

    /// <summary>添加审计中间件</summary>
    public CompleteAgentBuilder WithAudit(Core.Interfaces.IAuditLogRepository auditRepo)
    {
        _middlewares.Add(new AuditMiddleware(
            auditRepo,
            _loggerFactory.CreateLogger<AuditMiddleware>()));
        return this;
    }

    // ══════════════════════════════════════════════════════════════
    //  技能集成
    // ══════════════════════════════════════════════════════════════

    /// <summary>添加 FunctionTool 技能（通过 FunctionToolRegistry）</summary>
    public CompleteAgentBuilder WithFunctionTools(FunctionToolRegistry registry)
    {
        _functionToolRegistry = registry;
        return this;
    }

    /// <summary>添加 AgentSkill 技能（通过 UnifiedSkillProviderFactory）</summary>
    public CompleteAgentBuilder WithAgentSkills(UnifiedSkillProviderFactory factory)
    {
        _skillProviderFactory = factory;
        return this;
    }

    /// <summary>添加 MCP 工具技能</summary>
    public CompleteAgentBuilder WithMcpTools(McpSkillProvider provider)
    {
        _mcpSkillProvider = provider;
        return this;
    }

    // ══════════════════════════════════════════════════════════════
    //  构建
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建 CompleteAgent 实例
    /// </summary>
    public async Task<CompleteAgent> BuildAsync(CancellationToken ct = default)
    {
        if (_entity is null)
            throw new InvalidOperationException("Agent entity is required. Call WithEntity() first.");
        if (_chatClient is null)
            throw new InvalidOperationException("IChatClient is required. Call WithChatClient() first.");

        var logger = _loggerFactory.CreateLogger<CompleteAgent>();

        // 1. 构建中间件管道
        var pipeline = new MiddlewarePipeline(_middlewares, _loggerFactory.CreateLogger<MiddlewarePipeline>());

        // 2. 构建 ChatClientAgentOptions
        var options = _agentOptions ?? new Microsoft.Agents.AI.ChatClientAgentOptions
        {
            Name = _entity.Name,
            Description = _entity.Description,
            ChatOptions = new ChatOptions
            {
                Instructions = _entity.SystemPrompt,
                Temperature = _entity.Temperature.HasValue ? (float)_entity.Temperature.Value : null,
                MaxOutputTokens = _entity.MaxTokens,
                TopP = _entity.TopP.HasValue ? (float)_entity.TopP.Value : null
            },
            AIContextProviders = []
        };

        // 3. 添加 FunctionTool 工具
        if (_functionToolRegistry is not null)
        {
            var aiFunctions = await _functionToolRegistry.GetAIFunctionsForAgentAsync(_entity.Id, ct);
            if (options.ChatOptions is null) options.ChatOptions = new ChatOptions();
            options.ChatOptions.Tools ??= [];
            foreach (var fn in aiFunctions)
            {
                options.ChatOptions.Tools.Add(fn);
            }
            logger.LogInformation(
                "Added {Count} FunctionTools for agent [{Name}]",
                aiFunctions.Count, _entity.Name);
        }

        // 4. 添加 MCP 工具
        if (_mcpSkillProvider is not null)
        {
            var mcpTools = await _mcpSkillProvider.GetAIToolsForAgentAsync(_entity.Id, ct);
            if (options.ChatOptions is null) options.ChatOptions = new ChatOptions();
            options.ChatOptions.Tools ??= [];
            foreach (var tool in mcpTools)
            {
                options.ChatOptions.Tools.Add(tool);
            }
            logger.LogInformation(
                "Added {Count} MCP tools for agent [{Name}]",
                mcpTools.Count, _entity.Name);
        }

        // 5. 添加 AgentSkillsProvider
        if (_skillProviderFactory is not null)
        {
            var sp = _skillProviderFactory.CreateAgentSkillsProvider(_entity.Id, _loggerFactory);
            if (sp is not null)
            {
                options.AIContextProviders = [.. options.AIContextProviders ?? [], sp];
                logger.LogInformation(
                    "Created AgentSkillsProvider from factory for agent [{Name}]",
                    _entity.Name);
            }
        }

        // 6. 创建 MAF ChatClientAgent
        var innerAgent = new Microsoft.Agents.AI.ChatClientAgent(
            chatClient: _chatClient,
            options: options,
            loggerFactory: _loggerFactory,
            services: null);

        // 7. 创建 CompleteAgent
        var completeAgent = new CompleteAgent(
            _entity,
            innerAgent,
            pipeline,
            logger);

        logger.LogInformation("CompleteAgent [{Name}] built successfully with {MiddlewareCount} middlewares",
            _entity.Name, _middlewares.Count);

        return completeAgent;
    }
}
