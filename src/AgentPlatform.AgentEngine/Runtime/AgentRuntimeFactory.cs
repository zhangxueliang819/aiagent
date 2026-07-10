using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.AgentEngine.Providers;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.AgentEngine.Mcp;
using AgentPlatform.Application.Services;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// Agent 运行时工厂：将 Agent 实体（数据）包装为 MAF ChatClientAgent。
/// V2.0: 全面使用 MAF ChatClientAgent + IChatClient + AIFunction 工具，移除手动对话循环。
/// </summary>
public class AgentRuntimeFactory
{
    private readonly ILogger<AgentRuntimeFactory> _logger;
    private readonly ModelProviderFactory _modelProviderFactory;
    private readonly ModelRouter _modelRouter;
    private readonly FunctionToolRegistry _functionToolRegistry;
    private readonly UnifiedSkillProviderFactory _skillProviderFactory;
    private readonly ISessionRepository _sessionRepo;
    private readonly McpToolBridge? _mcpToolBridge;

    public AgentRuntimeFactory(
        ILogger<AgentRuntimeFactory> logger,
        ModelProviderFactory modelProviderFactory,
        ModelRouter modelRouter,
        FunctionToolRegistry functionToolRegistry,
        UnifiedSkillProviderFactory skillProviderFactory,
        ISessionRepository sessionRepo,
        McpToolBridge? mcpToolBridge = null)
    {
        _logger = logger;
        _modelProviderFactory = modelProviderFactory;
        _modelRouter = modelRouter;
        _functionToolRegistry = functionToolRegistry;
        _skillProviderFactory = skillProviderFactory;
        _sessionRepo = sessionRepo;
        _mcpToolBridge = mcpToolBridge;
    }

    /// <summary>
    /// 为指定 Agent 创建完整的 MAF ChatClientAgent。
    /// 连接 IChatClient + AIFunction 工具 + ChatOptions。
    /// </summary>
    public async Task<Microsoft.Agents.AI.ChatClientAgent> CreateChatClientAgentAsync(Agent entity, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating MAF ChatClientAgent for {AgentName} ({AgentId})", entity.Name, entity.Id);

        // 1. 创建 IChatClient（优先真实端点，回退到模拟）
        var chatClient = await ResolveChatClientAsync(entity, ct);

        // 2. 获取技能配置（用于注入 Instructions）
        var skillConfig = await _skillProviderFactory.GetSkillConfigurationAsync(entity.Id, ct);

        // 3. 构建 Tools 列表
        var tools = new List<AITool>();
        var aiTools = await _functionToolRegistry.GetAIToolsForAgentAsync(entity.Id, ct);
        tools.AddRange(aiTools);

        // 4. 构建增强的 System Instructions（包含技能上下文）
        var instructions = BuildEnhancedInstructions(entity, skillConfig);

        // 5. 创建 ChatClientAgent
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
    /// 为指定 Agent 创建完整的运行时上下文
    /// </summary>
    public async Task<AgentRuntimeContext> CreateContextAsync(Agent agent, CancellationToken ct = default)
    {
        _logger.LogInformation("Building runtime context for agent {AgentName} ({AgentId})",
            agent.Name, agent.Id);

        // 1. 创建 IChatClient（优先真实端点，回退到模拟）
        var llm = await ResolveChatClientAsync(agent, ct);

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
    /// 执行对话 V2.0: 使用 ChatClientAgent 内置 Agent Loop。
    /// </summary>
    public async Task<AgentResponse> RunAsync(
        Agent agent,
        Guid sessionId,
        string userMessage,
        CancellationToken ct = default)
    {
        // 1. 创建 MAF ChatClientAgent
        var mafAgent = await CreateChatClientAgentAsync(agent, ct);

        // 2. 构建消息列表
        var messages = await BuildMessagesAsync(agent, sessionId, userMessage, ct);

        // 3. 调用 ChatClientAgent.RunAsync（MAF 内置 Agent Loop + Tool Calling）
        _logger.LogInformation("Starting MAF agent run for {AgentName}", agent.Name);

        var response = await mafAgent.RunAsync(messages, session: null, options: null, ct);

        var content = response.Messages.LastOrDefault()?.Text ?? string.Empty;

        _logger.LogInformation("MAF agent run completed for {AgentName}, response length: {Length}",
            agent.Name, content.Length);

        return new AgentResponse
        {
            Content = content,
            ToolCallCount = 0,
            ModelName = null,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0)
        };
    }

    /// <summary>
    /// 流式执行对话 V2.0: 使用 IChatClient.GetStreamingResponseAsync 实现真流式输出。
    /// 每个 StreamingDelta 包含 content/thinking/tool_call 增量，前端通过 SSE 逐 token 渲染。
    /// </summary>
    public async IAsyncEnumerable<StreamingDelta> RunStreamingAsync(
        Agent agent,
        Guid sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. 获取 IChatClient（跳过 ChatClientAgent，直接用底层客户端流式）
        var chatClient = await ResolveChatClientAsync(agent, ct);

        // 2. 构建消息列表
        var messages = await BuildMessagesAsync(agent, sessionId, userMessage, ct);

        // 3. 构建 ChatOptions（含工具）
        var options = _modelProviderFactory.BuildChatOptions(agent) ?? new Microsoft.Extensions.AI.ChatOptions();
        var aiTools = await _functionToolRegistry.GetAIToolsForAgentAsync(agent.Id, ct);
        foreach (var t in aiTools)
            options.Tools.Add(t);

        // 4. 获取流式响应
        _logger.LogInformation("Starting streaming for {AgentName}", agent.Name);

        string fullContent = "";
        string fullThinking = "";
        int inputTokens = 0, outputTokens = 0;
        string? modelName = null;
        var toolCalls = new List<ToolCallInfo>();
        bool streamHadContent = false;
        bool usageReceived = false;

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, ct))
        {
            // 提取推理/思考内容
            string? thinkingDelta = null;
            if (update.AdditionalProperties?.TryGetValue("reasoning_content", out var r) == true)
                thinkingDelta = r?.ToString();

            if (!string.IsNullOrEmpty(thinkingDelta))
            {
                fullThinking += thinkingDelta;
                streamHadContent = true;
                yield return new StreamingDelta { Type = StreamDeltaType.Thinking, Thinking = thinkingDelta };
            }

            // 提取文本增量
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullContent += update.Text;
                streamHadContent = true;
                yield return new StreamingDelta { Type = StreamDeltaType.Token, Content = update.Text };
            }

            // 提取工具调用（FunctionCallContent）
            foreach (var fc in update.Contents.OfType<FunctionCallContent>())
            {
                var args = fc.Arguments is IDictionary<string, object?> dict
                    ? System.Text.Json.JsonSerializer.Serialize(dict)
                    : fc.Arguments?.ToString() ?? "{}";

                toolCalls.Add(new ToolCallInfo { Name = fc.Name, Arguments = args });
                streamHadContent = true;
                yield return new StreamingDelta
                {
                    Type = StreamDeltaType.ToolCall,
                    ToolCallName = fc.Name,
                    ToolCallArgs = args
                };
            }

            // 提取 Usage（流式模式下可能出现在最后一条 update）
            if (update.AdditionalProperties?.TryGetValue("usage", out var usageObj) == true
                && usageObj is System.Text.Json.JsonElement usageElem)
            {
                usageReceived = true;
                if (usageElem.TryGetProperty("prompt_tokens", out var pt))
                    inputTokens = pt.GetInt32();
                if (usageElem.TryGetProperty("completion_tokens", out var ct2))
                    outputTokens = ct2.GetInt32();
            }

            modelName ??= update.ModelId;
        }

        // 降级：流式无产出时回退到非流式调用
        if (!streamHadContent && toolCalls.Count == 0)
        {
            _logger.LogInformation("Streaming produced no content, falling back to non-streaming for {AgentName}", agent.Name);

            var response = await chatClient.GetResponseAsync(messages, options, ct);
            var text = response.Messages.LastOrDefault()?.Text ?? "";

            // 检查是否有 function call（通过 Contents）
            foreach (var fc in response.Messages.LastOrDefault()?.Contents.OfType<FunctionCallContent>() ?? [])
            {
                var args = fc.Arguments is IDictionary<string, object?> dict
                    ? System.Text.Json.JsonSerializer.Serialize(dict)
                    : fc.Arguments?.ToString() ?? "{}";
                toolCalls.Add(new ToolCallInfo { Name = fc.Name, Arguments = args });
                yield return new StreamingDelta
                {
                    Type = StreamDeltaType.ToolCall,
                    ToolCallName = fc.Name,
                    ToolCallArgs = args
                };
            }

            // 提取思考过程（DeepSeek-R1 等模型在非流式响应中返回 reasoning_content）
            if (response.AdditionalProperties?.TryGetValue("thinking", out var thinkObj) == true
                && thinkObj is string thinkStr && !string.IsNullOrEmpty(thinkStr))
            {
                fullThinking = thinkStr;
                yield return new StreamingDelta { Type = StreamDeltaType.Thinking, Thinking = thinkStr };
            }

            // 逐字符模拟流式输出（降级模式，添加延迟以支持前端逐字渲染）
            foreach (var ch in text)
            {
                fullContent += ch.ToString();
                yield return new StreamingDelta { Type = StreamDeltaType.Token, Content = ch.ToString() };
                // 小延迟让前端有时间逐字渲染（流式效果）
                await Task.Delay(10, ct);
            }

            modelName ??= response.ModelId;
            inputTokens = (int)(response.Usage?.InputTokenCount ?? 0);
            outputTokens = (int)(response.Usage?.OutputTokenCount ?? 0);
            usageReceived = true;
        }

        // 流式未提供 usage 时，尝试非流式回退获取 token 统计和模型名
        if (!usageReceived && agent.ModelEndpointId.HasValue)
        {
            try
            {
                _logger.LogInformation(
                    "Streaming did not provide usage, fallback to non-streaming for usage of {AgentName}",
                    agent.Name);

                var fallbackResponse = await chatClient.GetResponseAsync(messages, options, ct);
                if (fallbackResponse?.Usage != null)
                {
                    inputTokens = (int)(fallbackResponse.Usage?.InputTokenCount ?? 0);
                    outputTokens = (int)(fallbackResponse.Usage?.OutputTokenCount ?? 0);
                }
                modelName ??= fallbackResponse?.ModelId;
                usageReceived = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Non-streaming usage fallback also failed for {AgentName}", agent.Name);
            }
        }

        // 5. 发送完成事件（含聚合元信息）
        yield return new StreamingDelta
        {
            Type = StreamDeltaType.Done,
            Content = fullContent,
            Thinking = string.IsNullOrEmpty(fullThinking) ? null : fullThinking,
            ToolCallCount = toolCalls.Count,
            ToolCalls = toolCalls,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };

        _logger.LogInformation("Streaming completed for {AgentName}, {Tokens} tokens, {ThinkingLen} thinking chars",
            agent.Name, fullContent.Length, fullThinking.Length);
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

    /// <summary>
    /// 解析 IChatClient：优先使用 ModelRouter 获取真实端点，未配置则回退到默认（模拟）
    /// </summary>
    private async Task<IChatClient> ResolveChatClientAsync(Agent agent, CancellationToken ct)
    {
        // 尝试从 ModelRouter 获取真实 LLM 客户端
        var realClient = await _modelRouter.ResolveAsync(agent, ct);
        if (realClient is not null)
        {
            _logger.LogInformation("Using real LLM client for agent {AgentName}", agent.Name);
            return realClient;
        }

        // 回退到默认（模拟）客户端
        _logger.LogInformation("Agent {AgentName} has no model endpoint, using default (simulated) client", agent.Name);
        return await _modelProviderFactory.CreateChatClientAsync(agent);
    }

    /// <summary>
    /// 构建对话消息列表（System + 历史 + 新用户消息）
    /// </summary>
    private async Task<List<Microsoft.Extensions.AI.ChatMessage>> BuildMessagesAsync(
        Agent agent, Guid sessionId, string userMessage, CancellationToken ct)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>();

        // System message
        if (!string.IsNullOrEmpty(agent.SystemPrompt))
        {
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.System, agent.SystemPrompt));
        }

        // 历史对话
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        var history = session?.Conversations?.ToList() ?? new();
        foreach (var h in history.OrderBy(c => c.CreatedAt))
        {
            var role = h.Role.ToLower() switch
            {
                "user" => Microsoft.Extensions.AI.ChatRole.User,
                "assistant" => Microsoft.Extensions.AI.ChatRole.Assistant,
                _ => Microsoft.Extensions.AI.ChatRole.User
            };
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, h.Content));
        }

        // 用户消息
        messages.Add(new Microsoft.Extensions.AI.ChatMessage(
            Microsoft.Extensions.AI.ChatRole.User, userMessage));

        return messages;
    }
}

/// <summary>
/// Agent 运行时的完整上下文
/// </summary>
public class AgentRuntimeContext
{
    public Agent Agent { get; set; } = null!;
    public IChatClient ChatClient { get; set; } = null!;
    public Microsoft.Extensions.AI.ChatOptions? ChatOptions { get; set; }
    public AgentSkillConfiguration SkillConfig { get; set; } = null!;
}

/// <summary>
/// Agent 对话响应（V2.0 精简版）
/// </summary>
public class AgentResponse
{
    public string Content { get; set; } = string.Empty;
    public int ToolCallCount { get; set; }
    public string? Thinking { get; set; }
    public string? RawResponse { get; set; }
    public string? ModelName { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public List<ToolCallInfo> ToolCalls { get; set; } = new();
}

/// <summary>
/// 工具调用信息
/// </summary>
public class ToolCallInfo
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public string? Result { get; set; }
}

/// <summary>
/// 流式增量类型
/// </summary>
public enum StreamDeltaType
{
    Token,      // 文本增量
    Thinking,   // 推理/思考增量
    ToolCall,   // 工具调用
    Done        // 流结束
}

/// <summary>
/// 流式增量数据
/// </summary>
public class StreamingDelta
{
    public StreamDeltaType Type { get; set; }
    public string? Content { get; set; }       // Token 文本增量
    public string? Thinking { get; set; }       // 推理增量 / 累积完整思考
    public string? ToolCallName { get; set; }   // 工具名称
    public string? ToolCallArgs { get; set; }   // 工具参数 JSON
    public string? ToolCallResult { get; set; } // 工具结果

    // Done 事件附带元信息
    public int ToolCallCount { get; set; }
    public List<ToolCallInfo> ToolCalls { get; set; } = new();
    public string? ModelName { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
