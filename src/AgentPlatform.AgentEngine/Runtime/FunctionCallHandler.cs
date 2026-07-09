using System.Text.Json;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// Function Call 处理器：解析 LLM 返回的 function_call，
/// 根据函数名匹配 Skill 或 MCP Tool 并执行，返回结果给 LLM
/// </summary>
public class FunctionCallHandler
{
    private readonly ILogger<FunctionCallHandler> _logger;
    private readonly SkillDispatcher _skillDispatcher;

    public FunctionCallHandler(ILogger<FunctionCallHandler> logger, SkillDispatcher skillDispatcher)
    {
        _logger = logger;
        _skillDispatcher = skillDispatcher;
    }

    /// <summary>
    /// 处理 LLM 返回的 function_call
    /// </summary>
    /// <param name="functionName">LLM 请求调用的函数名</param>
    /// <param name="argumentsJson">函数参数的 JSON 字符串</param>
    /// <param name="agentSkills">Agent 关联的 Skill 列表</param>
    /// <param name="agentMcpTools">Agent 关联的 MCP Tool 列表（含 McpClient）</param>
    /// <param name="ct"></param>
    /// <returns>function_call 的执行结果（JSON 字符串），直接回传给 LLM</returns>
    public async Task<string> HandleAsync(
        string functionName,
        string argumentsJson,
        List<Skill> agentSkills,
        List<(McpTool Tool, Func<McpTool, Dictionary<string, object?>, CancellationToken, Task<string>> Invoker)> agentMcpTools,
        CancellationToken ct)
    {
        _logger.LogInformation("Handling function call: {FunctionName} with args: {Args}",
            functionName, argumentsJson);

        var arguments = string.IsNullOrWhiteSpace(argumentsJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson) ?? new();

        // 1. 匹配 Skill（函数名 = Skill.Name 或 skill_{id}）
        var skill = agentSkills.FirstOrDefault(s =>
            s.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase) ||
            $"skill_{s.Id.ToString("N")[..8]}" == functionName);

        if (skill is not null)
        {
            _logger.LogInformation("Matched Skill: {SkillName}({Type})", skill.Name, skill.Type);
            var result = await _skillDispatcher.ExecuteSkillAsync(skill, arguments, ct);
            return result.Success
                ? result.Data ?? "{}"
                : JsonSerializer.Serialize(new { error = result.Error });
        }

        // 2. 匹配 MCP Tool
        var mcpMatch = agentMcpTools.FirstOrDefault(m =>
            m.Tool.ToolName.Equals(functionName, StringComparison.OrdinalIgnoreCase));

        if (mcpMatch != default)
        {
            _logger.LogInformation("Matched MCP Tool: {ToolName}", mcpMatch.Tool.ToolName);
            try
            {
                var content = await mcpMatch.Invoker(mcpMatch.Tool, arguments, ct);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP tool invocation failed");
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        // 3. 未匹配到任何工具
        _logger.LogWarning("No matching function found for: {FunctionName}. Available: {Skills}, {Mcp}",
            functionName,
            string.Join(", ", agentSkills.Select(s => s.Name)),
            string.Join(", ", agentMcpTools.Select(m => m.Tool.ToolName)));

        return JsonSerializer.Serialize(new
        {
            error = $"Function '{functionName}' not found. Available: {string.Join(", ", agentSkills.Select(s => s.Name).Concat(agentMcpTools.Select(m => m.Tool.ToolName)))}"
        });
    }
}
