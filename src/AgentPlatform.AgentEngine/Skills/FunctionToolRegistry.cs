using System.Text.Json;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// FunctionTool 注册表：将数据库中 Type=FunctionTool 的 Skill 转换为 MAF AIFunction。
/// 使用 Microsoft.Extensions.AI.AIFunctionFactory 创建标准 AIFunction 实例，
/// 注入到 ChatClientAgent 的 ChatOptions.Tools。
/// </summary>
public class FunctionToolRegistry
{
    private readonly ISkillRepository _skillRepo;
    private readonly IAgentSkillRepository _agentSkillRepo;
    private readonly ILogger<FunctionToolRegistry> _logger;

    public FunctionToolRegistry(
        ISkillRepository skillRepo,
        IAgentSkillRepository agentSkillRepo,
        ILogger<FunctionToolRegistry> logger)
    {
        _skillRepo = skillRepo;
        _agentSkillRepo = agentSkillRepo;
        _logger = logger;
    }

    /// <summary>
    /// 获取 Agent 绑定的 FunctionTool 类型技能
    /// </summary>
    public async Task<List<Skill>> GetFunctionToolsForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var bindings = await _agentSkillRepo.GetByAgentIdAsync(agentId, ct);
        var skillIds = bindings
            .Where(b => b.IsEnabled)
            .OrderBy(b => b.Priority)
            .Select(b => b.SkillId)
            .ToList();

        var tools = new List<Skill>();
        foreach (var skillId in skillIds)
        {
            var skill = await _skillRepo.GetByIdAsync(skillId, ct);
            if (skill is not null
                && skill.Type == SkillType.FunctionTool
                && skill.IsEnabled)
            {
                tools.Add(skill);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} FunctionTools for agent {AgentId}",
            tools.Count, agentId);

        return tools;
    }

    /// <summary>
    /// 执行指定的 FunctionTool 技能
    /// V2.0: 直接基于 Skill.Implementation 执行业务逻辑，不再通过 SkillDispatcher 分派
    /// </summary>
    public async Task<string> ExecuteAsync(string skillName, string arguments, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing FunctionTool: {SkillName} with args: {Args}",
            skillName, arguments);

        // 从数据库加载技能定义以获取 Implementation
        var skills = await _skillRepo.GetAllAsync(ct);
        var skill = skills.FirstOrDefault(s => s.Name == skillName && s.Type == SkillType.FunctionTool);

        if (skill is null)
        {
            _logger.LogWarning("FunctionTool {Name} not found in database", skillName);
            return JsonSerializer.Serialize(new { error = $"Skill '{skillName}' not found" });
        }

        var parameters = string.IsNullOrWhiteSpace(arguments)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments) ?? new();

        try
        {
            // 执行技能逻辑（基于 Implementation 字段）
            var result = await ExecuteSkillImplementationAsync(skill, parameters, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FunctionTool {Name} execution failed", skillName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 执行技能实现逻辑
    /// 根据 Implementation 内容执行（支持简单 JSON 模板和脚本）
    /// </summary>
    private static Task<string> ExecuteSkillImplementationAsync(Skill skill, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        var impl = skill.Implementation?.Trim() ?? "{}";

        // 尝试解析 Implementation 为 JSON 模板
        if (impl.StartsWith("{") || impl.StartsWith("["))
        {
            try
            {
                using var doc = JsonDocument.Parse(impl);
                // 将模板中的占位符替换为实际参数值
                var result = ReplaceTemplatePlaceholders(impl, parameters);
                return Task.FromResult(result);
            }
            catch { /* 非 JSON，按脚本处理 */ }
        }

        // 对于脚本类型，返回参数回显（实际场景可接入脚本引擎）
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            skill = skill.Name,
            invoked_with = parameters,
            message = $"Skill '{skill.Name}' executed with provided arguments"
        }));
    }

    /// <summary>
    /// 替换 JSON 模板中的参数占位符 {{paramName}}
    /// </summary>
    private static string ReplaceTemplatePlaceholders(string template, Dictionary<string, object?> parameters)
    {
        var result = template;
        foreach (var (key, value) in parameters)
        {
            var placeholder = $"{{{{{key}}}}}";
            if (result.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                var replacement = value switch
                {
                    string s => s,
                    JsonElement je => je.GetRawText(),
                    _ => value?.ToString() ?? "null"
                };
                result = result.Replace(placeholder, replacement, StringComparison.OrdinalIgnoreCase);
            }
        }
        return result;
    }

    /// <summary>
    /// 将 FunctionTool Skill 列表转换为 MAF AIFunction 列表。
    /// 使用 AIFunctionFactory.Create 自动从委托生成 JSON Schema。
    /// </summary>
    public async Task<List<AIFunction>> GetAIFunctionsForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var skills = await GetFunctionToolsForAgentAsync(agentId, ct);

        return skills.Select(s => AIFunctionFactory.Create(
            method: (string arguments, CancellationToken ct2) => ExecuteAsync(s.Name, arguments, ct2),
            name: s.Name,
            description: s.Description
        )).ToList();
    }

    /// <summary>
    /// 获取 MAF AITool 列表（可直接注入 ChatOptions.Tools）
    /// </summary>
    public async Task<List<AITool>> GetAIToolsForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var functions = await GetAIFunctionsForAgentAsync(agentId, ct);
        return functions.Select(f => (AITool)f).ToList();
    }

    /// <summary>
    /// 将 FunctionTool Skill 列表转换为 OpenAI 兼容的 Tool 定义（兼容旧接口）
    /// </summary>
    public static List<object> BuildToolDefinitions(List<Skill> tools)
    {
        return tools.Select(skill =>
        {
            var schema = new Dictionary<string, object?>();
            try
            {
                schema = JsonSerializer.Deserialize<Dictionary<string, object?>>(skill.InputSchema) ?? new();
            }
            catch { /* 使用空 schema */ }

            return (object)new Dictionary<string, object?>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = skill.Name,
                    ["description"] = skill.Description,
                    ["parameters"] = schema
                }
            };
        }).ToList();
    }
}
