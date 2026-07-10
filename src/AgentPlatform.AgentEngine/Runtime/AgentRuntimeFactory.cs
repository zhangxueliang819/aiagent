using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.AgentEngine.Providers;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.AgentEngine.Mcp;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// Agent 运行时工厂：将 Agent 实体（数据）包装为 MAF ChatClientAgent。
///
/// 双轨架构核心：
///   - Agent 实体：存储元数据和配置（数据库）
///   - Agent 运行时：由本工厂创建 MAF ChatClientAgent + IChatClient + AIFunction 工具
///
/// MAF 阶段：创建 ChatClientAgent 实例，由 MAF 内置 Agent Loop 替代手动循环。
/// </summary>
public class AgentRuntimeFactory
{
    private readonly ILogger<AgentRuntimeFactory> _logger;
    private readonly ModelProviderFactory _modelProviderFactory;
    private readonly FunctionToolRegistry _functionToolRegistry;
    private readonly UnifiedSkillProviderFactory _skillProviderFactory;
    private readonly AgentRuntime _agentRuntime;
    private readonly ISessionRepository _sessionRepo;
    private readonly IModelProvider _defaultProvider;
    private readonly McpToolBridge? _mcpToolBridge;

    public AgentRuntimeFactory(
        ILogger<AgentRuntimeFactory> logger,
        ModelProviderFactory modelProviderFactory,
        FunctionToolRegistry functionToolRegistry,
        UnifiedSkillProviderFactory skillProviderFactory,
        AgentRuntime agentRuntime,
        ISessionRepository sessionRepo,
        IModelProvider defaultProvider,
        McpToolBridge? mcpToolBridge = null)
    {
        _logger = logger;
        _modelProviderFactory = modelProviderFactory;
        _functionToolRegistry = functionToolRegistry;
        _skillProviderFactory = skillProviderFactory;
        _agentRuntime = agentRuntime;
        _sessionRepo = sessionRepo;
        _defaultProvider = defaultProvider;
        _mcpToolBridge = mcpToolBridge;
    }

    /// <summary>
    /// 为指定 Agent 创建完整的 MAF ChatClientAgent。
    /// 连接 IChatClient + AIFunction 工具 + ChatOptions。
    /// </summary>
    public async Task<Microsoft.Agents.AI.ChatClientAgent> CreateChatClientAgentAsync(Agent entity, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating MAF ChatClientAgent for {AgentName} ({AgentId})", entity.Name, entity.Id);

        // 1. 创建 IChatClient
        var chatClient = await _modelProviderFactory.CreateChatClientAsync(entity);

        // 2. 获取技能配置（用于注入 Instructions）
        var skillConfig = await _skillProviderFactory.GetSkillConfigurationAsync(entity.Id, ct);

        // 3. 构建 Tools 列表
        var tools = new List<AITool>();
        var aiTools = await _functionToolRegistry.GetAIToolsForAgentAsync(entity.Id, ct);
        tools.AddRange(aiTools);

        // 4. 构建增强的 System Instructions（包含技能上下文）
        var instructions = BuildEnhancedInstructions(entity, skillConfig);

        // 5. 构建 ChatOptions
        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            Temperature = entity.Temperature.HasValue ? (float)entity.Temperature.Value : null,
            MaxOutputTokens = entity.MaxTokens,
            TopP = entity.TopP.HasValue ? (float)entity.TopP.Value : null,
            Instructions = instructions,
        };
        foreach (var tool in tools)
            chatOptions.Tools.Add(tool);

        // 6. 创建 ChatClientAgent
        var agent = new Microsoft.Agents.AI.ChatClientAgent(
            chatClient: chatClient,
            instructions: instructions,
            name: entity.Name,
            description: entity.Description,
            tools: tools);

        _logger.LogInformation("MAF ChatClientAgent created for {AgentName} with {ToolCount} tools",
            entity.Name, tools.Count);

        return agent;
    }

    /// <summary>
    /// 为指定 Agent 创建完整的运行时上下文（兼容旧接口）
    /// </summary>
    public async Task<AgentRuntimeContext> CreateContextAsync(Agent agent, CancellationToken ct = default)
    {
        _logger.LogInformation("Building runtime context for agent {AgentName} ({AgentId})",
            agent.Name, agent.Id);

        // 1. 创建 IChatClient（MAF 标准接口）
        var llm = await _modelProviderFactory.CreateChatClientAsync(agent);

        // 2. 获取技能配置
        var skillConfig = await _skillProviderFactory.GetSkillConfigurationAsync(agent.Id, ct);

        // 3. 构建 AIFunction 工具
        var aiTools = await _functionToolRegistry.GetAIToolsForAgentAsync(agent.Id, ct);

        // 4. 构建 ChatOptions
        var chatOptions = _modelProviderFactory.BuildChatOptions(agent);
        if (chatOptions is not null)
        {
            chatOptions.Instructions = BuildEnhancedInstructions(agent, skillConfig);
            foreach (var t in aiTools)
                chatOptions.Tools.Add(t);
        }

        return new AgentRuntimeContext
        {
            Agent = agent,
            ChatClient = llm,
            ChatOptions = chatOptions,
            SkillConfig = skillConfig
        };
    }

    /// <summary>
    /// 执行对话（当前阶段：委托给 AgentRuntime.RunAsync）。
    /// MAF 阶段：使用 ChatClientAgent.RunAsync()。
    /// </summary>
    public async Task<AgentResponse> RunAsync(
        Agent agent,
        Guid sessionId,
        string userMessage,
        CancellationToken ct = default)
    {
        var ctx = await CreateContextAsync(agent, ct);

        // 获取对话历史 (从 SessionRepository)
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        var history = session?.Conversations?.ToList() ?? new();

        // 当前阶段：使用现有的 AgentRuntime 手动循环
        // 从 IChatClient 适配器提取 IModelProvider 以兼容旧 AgentRuntime
        var legacyProvider = (ctx.ChatClient as Providers.ModelProviderChatClientAdapter)
            ?.GetService(typeof(IModelProvider)) as IModelProvider;

        return await _agentRuntime.RunAsync(
            agent,
            legacyProvider ?? _defaultProvider,
            history,
            userMessage,
            ctx.SkillConfig.FunctionTools,
            new(), // MCP endpoints from agent
            ct);
    }

    /// <summary>
    /// 构建增强的 System Instructions（含技能上下文注入）
    /// </summary>
    private static string BuildEnhancedInstructions(Agent agent, AgentSkillConfiguration skillConfig)
    {
        if (skillConfig.InlineAgentSkills.Count == 0 && skillConfig.FileSkillPaths.Count == 0)
            return agent.SystemPrompt ?? string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(agent.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("## 可用知识技能");

        foreach (var skill in skillConfig.InlineAgentSkills)
        {
            sb.AppendLine($"### {skill.Name}: {skill.Description}");
            if (!string.IsNullOrEmpty(skill.Implementation))
            {
                var truncated = skill.Implementation.Length > 2000
                    ? skill.Implementation[..2000] + "\n...(已截断)"
                    : skill.Implementation;
                sb.AppendLine(truncated);
            }
            sb.AppendLine();
        }

        foreach (var path in skillConfig.FileSkillPaths)
        {
            sb.AppendLine($"- 文件技能路径: {path}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Agent 运行时的完整上下文
/// </summary>
public class AgentRuntimeContext
{
    /// <summary>Agent 实体（数据）</summary>
    public Agent Agent { get; set; } = null!;

    /// <summary>MAF 标准 IChatClient</summary>
    public IChatClient ChatClient { get; set; } = null!;

    /// <summary>MAF ChatOptions（Temperature, MaxTokens, Instructions, Tools）</summary>
    public Microsoft.Extensions.AI.ChatOptions? ChatOptions { get; set; }

    /// <summary>技能配置（FunctionTools + AgentSkills + FilePaths）</summary>
    public AgentSkillConfiguration SkillConfig { get; set; } = null!;
}
