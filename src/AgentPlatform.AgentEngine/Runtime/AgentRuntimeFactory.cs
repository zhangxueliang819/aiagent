using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.AgentEngine.Providers;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.Application.Services;
using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// Agent 运行时工厂：将 Agent 实体（数据）包装为 MAF ChatClientAgent。
/// V2.0: 全面使用 MAF ChatClientAgent + IChatClient + AIFunction 工具，移除手动对话循环。
/// </summary>
public class AgentRuntimeFactory
{
    private readonly ILogger<AgentRuntimeFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ModelProviderFactory _modelProviderFactory;
    private readonly ModelRouter _modelRouter;
    private readonly FunctionToolRegistry _functionToolRegistry;
    private readonly UnifiedSkillProviderFactory _skillProviderFactory;

    public AgentRuntimeFactory(
        ILogger<AgentRuntimeFactory> logger,
        ILoggerFactory loggerFactory,
        ModelProviderFactory modelProviderFactory,
        ModelRouter modelRouter,
        FunctionToolRegistry functionToolRegistry,
        UnifiedSkillProviderFactory skillProviderFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _modelProviderFactory = modelProviderFactory;
        _modelRouter = modelRouter;
        _functionToolRegistry = functionToolRegistry;
        _skillProviderFactory = skillProviderFactory;
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

        // 2. 构建消息列表（System + 用户消息）
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(agent.SystemPrompt))
            messages.Add(new ChatMessage(ChatRole.System, agent.SystemPrompt));
        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        // 3. 构建 ChatOptions（含工具）
        var options = _modelProviderFactory.BuildChatOptions(agent) ?? new Microsoft.Extensions.AI.ChatOptions();
        var aiTools = await _functionToolRegistry.GetAIToolsForAgentAsync(agent.Id, ct);
        foreach (var t in aiTools)
            (options.Tools ??= []).Add(t);

        // 4. 获取流式响应
        _logger.LogInformation("Starting streaming for {AgentName}", agent.Name);

        string fullContent = "";
        string fullThinking = "";
        int inputTokens = 0, outputTokens = 0;
        string? modelName = null;
        var toolCalls = new List<ToolCallInfo>();

        // 收集所有原始响应元数据
        var rawAdditionalProps = new Dictionary<string, object?>();
        string? rawResponseId = null;
        string? rawFinishReason = null;

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, ct))
        {
            // 收集原始响应属性（逐条合并，后面的覆盖前面的）
            rawResponseId ??= update.ResponseId;
            rawFinishReason ??= update.FinishReason?.ToString();
            if (update.AdditionalProperties is { Count: > 0 })
            {
                foreach (var kv in update.AdditionalProperties)
                    rawAdditionalProps[kv.Key] = kv.Value;
            }
            // 提取推理/思考内容
            string? thinkingDelta = null;
            if (update.AdditionalProperties?.TryGetValue("reasoning_content", out var r) == true)
                thinkingDelta = r?.ToString();

            if (!string.IsNullOrEmpty(thinkingDelta))
            {
                fullThinking += thinkingDelta;
                yield return new StreamingDelta { Type = StreamDeltaType.Thinking, Thinking = thinkingDelta };
            }

            // 提取文本增量
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullContent += update.Text;
                yield return new StreamingDelta { Type = StreamDeltaType.Token, Content = update.Text };
            }

            // 提取工具调用（FunctionCallContent）
            foreach (var fc in update.Contents.OfType<FunctionCallContent>())
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

            // 提取 Usage（流式模式下可能出现在最后一条 update）
            if (update.AdditionalProperties?.TryGetValue("usage", out var usageObj) == true
                && usageObj is System.Text.Json.JsonElement usageElem)
            {
                if (usageElem.TryGetProperty("prompt_tokens", out var pt))
                    inputTokens = pt.GetInt32();
                if (usageElem.TryGetProperty("completion_tokens", out var ct2))
                    outputTokens = ct2.GetInt32();
            }

            modelName ??= update.ModelId;
        }

        // 5. 构建原始响应 JSON（包含模型返回的所有元信息）
        var rawResponseObj = new Dictionary<string, object?>
        {
            ["modelId"] = modelName,
            ["responseId"] = rawResponseId,
            ["finishReason"] = rawFinishReason,
            ["inputTokens"] = inputTokens,
            ["outputTokens"] = outputTokens,
            ["text"] = fullContent,
            ["thinking"] = string.IsNullOrEmpty(fullThinking) ? null : fullThinking
        };
        if (toolCalls.Count > 0)
        {
            rawResponseObj["toolCalls"] = toolCalls.Select(tc => new
            {
                name = tc.Name,
                arguments = tc.Arguments,
                result = tc.Result
            }).ToList();
        }
        if (rawAdditionalProps.Count > 0)
        {
            // 排除已在顶层暴露的字段，避免重复
            var filtered = rawAdditionalProps
                .Where(kv => kv.Key is not ("reasoning_content" or "usage"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            if (filtered.Count > 0)
                rawResponseObj["additionalProperties"] = filtered;
        }
        var rawResponseJson = JsonSerializer.Serialize(rawResponseObj, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        // 6. 发送完成事件（含聚合元信息）
        yield return new StreamingDelta
        {
            Type = StreamDeltaType.Done,
            Content = fullContent,
            Thinking = string.IsNullOrEmpty(fullThinking) ? null : fullThinking,
            ToolCallCount = toolCalls.Count,
            ToolCalls = toolCalls,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            RawResponse = rawResponseJson
        };

        _logger.LogInformation("Streaming completed for {AgentName}, {Tokens} tokens, {ThinkingLen} thinking chars",
            agent.Name, fullContent.Length, fullThinking.Length);
    }

    /// <summary>
    /// 构建增强的 System Instructions（含技能上下文注入）
    /// - Inline AgentSkill：全量 Markdown 指令注入 Instructions
    /// - File/Directory AgentSkill：仅宣告名称 + 描述（渐进式披露由 AgentSkillsProvider 处理）
    /// </summary>
    private static string BuildEnhancedInstructions(Agent agent, AgentSkillConfiguration skillConfig)
    {
        var hasInline = skillConfig.InlineAgentSkills.Count > 0;
        var hasFile = skillConfig.FileAgentSkills.Count > 0;
        if (!hasInline && !hasFile)
            return agent.SystemPrompt ?? string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(agent.SystemPrompt);
        sb.AppendLine();

        // Inline AgentSkill：全量注入
        if (hasInline)
        {
            sb.AppendLine("## 可用知识技能（内联）");
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
        }

        // File/Directory AgentSkill：仅宣告名称 + 描述（减少 token 消耗）
        // 完整内容由 MAF AgentSkillsProvider 通过 load_skill 工具按需加载
        if (hasFile)
        {
            sb.AppendLine("## 可用文件技能（按需加载）");
            sb.AppendLine("当你的任务与以下技能领域匹配时，使用 `load_skill` 工具加载完整的技能说明：");
            foreach (var skill in skillConfig.FileAgentSkills)
            {
                sb.AppendLine($"- **{skill.Name}**: {skill.Description}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 解析 IChatClient：优先使用 ModelRouter 获取真实端点，未配置则回退到默认（模拟）
    /// </summary>
    private async Task<IChatClient> ResolveChatClientAsync(Agent agent, CancellationToken ct)
    {
        // 从 ModelRouter 获取真实 LLM 客户端
        var client = await _modelRouter.ResolveAsync(agent, ct);
        return client ?? throw new InvalidOperationException(
            $"No IChatClient resolved for agent '{agent.Name}' (Id: {agent.Id}). " +
            "Ensure the agent has a valid ModelEndpoint configured.");
    }

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
    public string? RawResponse { get; set; }
}
