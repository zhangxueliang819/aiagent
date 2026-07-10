using System.Text.Json;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine.Context;

/// <summary>
/// Agent 上下文提供器：构建结构化的 System Prompt。
///
/// MAF 1.13.0 架构说明：
/// SystemPrompt/Instructions 通过 ChatClientAgentOptions.Instructions 或 ChatOptions.Instructions 注入。
/// 无需实现 AIContextProvider 基类（MAF 中 AgentSkillsSource/AIContextProvider 已废弃），
/// 上下文直接拼接到 Instructions 字符串中。
/// </summary>
public class AgentContextProvider
{
    /// <summary>
    /// 构建完整的 System Message
    /// </summary>
    /// <param name="agent">Agent 实体</param>
    /// <param name="skillConfig">技能配置</param>
    /// <param name="mcpTools">MCP 工具列表</param>
    /// <param name="dynamicContext">动态上下文（如用户信息、时间等）</param>
    /// <returns>完整的 System Prompt 字符串</returns>
    public string BuildSystemMessage(
        Agent agent,
        AgentSkillConfiguration skillConfig,
        List<McpTool> mcpTools,
        Dictionary<string, string>? dynamicContext = null)
    {
        var sb = new System.Text.StringBuilder();

        // 1. Agent 的核心 SystemPrompt
        sb.AppendLine(agent.SystemPrompt);
        sb.AppendLine();

        // 2. 动态上下文
        if (dynamicContext is { Count: > 0 })
        {
            sb.AppendLine("## 当前上下文");
            foreach (var (key, value) in dynamicContext)
                sb.AppendLine($"- {key}: {value}");
            sb.AppendLine();
        }

        // 3. AgentSkill（知识技能）—— 作为上下文注入而非 function call
        if (skillConfig.InlineAgentSkills.Count > 0)
        {
            sb.AppendLine("## 可用知识技能（按需查阅）");
            foreach (var skill in skillConfig.InlineAgentSkills)
            {
                sb.AppendLine($"### {skill.Name}: {skill.Description}");
                if (!string.IsNullOrEmpty(skill.Implementation))
                {
                    // 内联 AgentSkill 的 Markdown 指令直接嵌入
                    var truncated = skill.Implementation.Length > 2000
                        ? skill.Implementation[..2000] + "\n...(已截断)"
                        : skill.Implementation;
                    sb.AppendLine(truncated);
                }
                sb.AppendLine();
            }
        }

        // 4. Function 工具定义
        if (skillConfig.ToolDefinitions.Count > 0 || mcpTools.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine("你有以下可调用的函数。如需调用，请返回 JSON 格式：");
            sb.AppendLine("{\"name\": \"函数名\", \"arguments\": { ... }}");
            sb.AppendLine();

            foreach (var toolDef in skillConfig.ToolDefinitions)
            {
                sb.AppendLine(JsonSerializer.Serialize(toolDef));
            }

            foreach (var tool in mcpTools.Where(t => t.IsEnabled))
            {
                sb.AppendLine(JsonSerializer.Serialize(new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.ToolName,
                        description = tool.Description,
                        parameters = TryParseJson(tool.InputSchema)
                    }
                }));
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("如果用户请求需要使用函数，请返回函数调用的 JSON。否则自然回答。");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 构建简化的 Tool Definitions（用于 Chat API 的 tools 参数，非 Prompt 内联）
    /// </summary>
    public List<object> BuildToolDefinitions(AgentSkillConfiguration skillConfig, List<McpTool> mcpTools)
    {
        var tools = new List<object>(skillConfig.ToolDefinitions);

        foreach (var tool in mcpTools.Where(t => t.IsEnabled))
        {
            tools.Add(new Dictionary<string, object?>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = tool.ToolName,
                    ["description"] = tool.Description,
                    ["parameters"] = TryParseJson(tool.InputSchema)
                }
            });
        }

        return tools;
    }

    private static object TryParseJson(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json) ?? new { }; }
        catch { return new { }; }
    }
}
