using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.ModelProviders.Mcp;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// Agent 运行时编排器：核心对话循环
/// 
/// 流程：用户消息 → 构建 SystemPrompt + 工具定义 → 调 LLM →
///       如果 function_call → FunctionCallHandler 执行 → 结果回传 LLM →
///       循环直到 LLM 返回纯文本响应
/// </summary>
public class AgentRuntime
{
    private readonly ILogger<AgentRuntime> _logger;
    private readonly FunctionCallHandler _functionHandler;
    private readonly McpClient _mcpClient;

    /// <summary>最大 Function Call 循环次数，防止无限循环</summary>
    private const int MaxToolCalls = 10;

    public AgentRuntime(ILogger<AgentRuntime> logger, FunctionCallHandler functionHandler, McpClient mcpClient)
    {
        _logger = logger;
        _functionHandler = functionHandler;
        _mcpClient = mcpClient;
    }

    /// <summary>
    /// 执行 Agent 对话（非流式），返回最终响应
    /// </summary>
    public async Task<AgentResponse> RunAsync(
        Agent agent,
        IModelProvider llm,
        List<Conversation> history,
        string userMessage,
        List<Skill> skills,
        List<McpEndpoint> mcpEndpoints,
        CancellationToken ct)
    {
        _logger.LogInformation("AgentRuntime starting for Agent: {AgentName} with {SkillCount} skills, {McpCount} MCPs",
            agent.Name, skills.Count, mcpEndpoints.Count);

        // 为 MCP 端点发现工具
        var mcpTools = new List<McpTool>();
        foreach (var endpoint in mcpEndpoints.Where(e => e.IsEnabled))
        {
            var tools = await _mcpClient.DiscoverToolsAsync(endpoint, ct);
            mcpTools.AddRange(tools);
        }
        _logger.LogInformation("Total MCP tools discovered: {Count}", mcpTools.Count);

        // 构建 MCP 工具调用委托列表
        var mcpInvokers = mcpTools.Select(t => (
            Tool: t,
            Invoker: new Func<McpTool, Dictionary<string, object?>, CancellationToken, Task<string>>(
                async (tool, args, ct2) =>
                {
                    var result = await _mcpClient.InvokeToolAsync(tool, args, ct2);
                    return result.Content;
                })
        )).ToList();

        // 构建消息列表
        var messages = new List<ChatMessage>();

        // System message: 包含 SystemPrompt + 所有工具定义
        var systemContent = BuildSystemMessage(agent, skills, mcpTools);
        messages.Add(new ChatMessage { Role = "system", Content = systemContent });

        // 历史对话
        foreach (var h in history.OrderBy(c => c.CreatedAt))
        {
            messages.Add(new ChatMessage { Role = h.Role, Content = h.Content });
        }

        // 用户消息
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        // 对话循环
        int toolCallCount = 0;
        string? finalContent = null;

        while (toolCallCount < MaxToolCalls)
        {
            var request = new ChatCompletionRequest
            {
                ModelId = agent.ModelId,
                Messages = messages,
                MaxTokens = agent.MaxTokens ?? 4096,
                Temperature = (float)(agent.Temperature ?? 0.7),
                TopP = agent.TopP is not null ? (float)agent.TopP : null
            };

            var response = await llm.CompleteAsync(request, ct);
            _logger.LogInformation("LLM response received, tokens: {Input}/{Output}",
                response.InputTokens, response.OutputTokens);

            // 尝试解析 function_call
            var (isFunctionCall, functionName, functionArgs) = TryParseFunctionCall(response.Content);

            if (!isFunctionCall)
            {
                // 纯文本响应，对话结束
                finalContent = response.Content;
                break;
            }

            // 执行 function call
            toolCallCount++;
            _logger.LogInformation("Function call #{Count}: {Function}({Args})",
                toolCallCount, functionName, functionArgs);

            var toolResult = await _functionHandler.HandleAsync(
                functionName!,
                functionArgs ?? "{}",
                skills,
                mcpInvokers,
                ct);

            // 将 function_call 结果添加到消息历史
            messages.Add(new ChatMessage { Role = "assistant", Content = response.Content });
            messages.Add(new ChatMessage { Role = "function", Content = toolResult });

            _logger.LogInformation("Tool result length: {Length} chars", toolResult.Length);
        }

        if (finalContent is null)
        {
            finalContent = $"已达到最大工具调用次数 ({MaxToolCalls})，请简化您的问题。";
        }

        _logger.LogInformation("AgentRuntime completed. Tool calls: {Count}, Final response length: {Length}",
            toolCallCount, finalContent.Length);

        return new AgentResponse
        {
            Content = finalContent,
            ToolCallCount = toolCallCount
        };
    }

    /// <summary>
    /// 流式对话（简化版：先执行 function call 循环，最后流式返回最终文本）
    /// </summary>
    public async IAsyncEnumerable<string> RunStreamAsync(
        Agent agent,
        IModelProvider llm,
        List<Conversation> history,
        string userMessage,
        List<Skill> skills,
        List<McpEndpoint> mcpEndpoints,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var response = await RunAsync(agent, llm, history, userMessage, skills, mcpEndpoints, ct);

        // 按字符流式输出（真实场景应对接 LLM 的 streaming API）
        foreach (var ch in response.Content)
        {
            yield return ch.ToString();
            await Task.Delay(20, ct); // 模拟打字效果
        }
    }

    /// <summary>
    /// 解析 LLM 响应是否为 function_call
    /// 当前使用简单的 JSON 模式匹配
    /// </summary>
    private static (bool IsCall, string? Name, string? Args) TryParseFunctionCall(string content)
    {
        // 尝试匹配格式: {"name":"function_name","arguments":{...}}
        // 或者: ```json\n{"name":"...","arguments":{...}}\n```
        try
        {
            var json = content.Trim();
            // 去 markdown 代码块
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("name", out var nameProp) &&
                root.TryGetProperty("arguments", out var argsProp))
            {
                return (true, nameProp.GetString(), argsProp.GetRawText());
            }
        }
        catch { /* 不是 JSON，视为普通文本响应 */ }

        return (false, null, null);
    }

    /// <summary>
    /// 构建 System Message：Agent 的 SystemPrompt + 所有可用工具/技能的 Function Schema
    /// </summary>
    private static string BuildSystemMessage(Agent agent, List<Skill> skills, List<McpTool> mcpTools)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(agent.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("You have access to the following functions. To call a function, respond with a JSON object:");
        sb.AppendLine("{\"name\": \"function_name\", \"arguments\": { ... }}");
        sb.AppendLine();

        // Skill 定义
        foreach (var skill in skills.Where(s => s.IsEnabled))
        {
            sb.AppendLine($"## Skill: {skill.Name}");
            sb.AppendLine($"Description: {skill.Description}");
            sb.AppendLine($"Type: {skill.Type}");
            sb.AppendLine($"Schema: {skill.InputSchema}");
            sb.AppendLine();
        }

        // MCP Tool 定义
        foreach (var tool in mcpTools.Where(t => t.IsEnabled))
        {
            sb.AppendLine($"## Tool: {tool.ToolName}");
            sb.AppendLine($"Description: {tool.Description}");
            sb.AppendLine($"Schema: {tool.InputSchema}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("If the user's request requires using a function, respond ONLY with the JSON function call. Otherwise, respond naturally.");

        return sb.ToString();
    }
}

public class AgentResponse
{
    public string Content { get; set; } = string.Empty;
    public int ToolCallCount { get; set; }
}
