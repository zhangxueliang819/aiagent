using System.Text.Json;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// FunctionTool 注册表：将数据库中 Type=FunctionTool 的 Skill 转换为 MAF AIFunction。
/// 运行时通过 FunctionSkillRegistry 解析实际执行委托，而非模拟执行。
/// 使用 Microsoft.Extensions.AI.AIFunctionFactory 创建标准 AIFunction 实例，
/// 注入到 ChatClientAgent 的 ChatOptions.Tools。
/// </summary>
public class FunctionToolRegistry
{
    private readonly ISkillRepository _skillRepo;
    private readonly IAgentSkillRepository _agentSkillRepo;
    private readonly FunctionSkillRegistry _executorRegistry;
    private readonly ILogger<FunctionToolRegistry> _logger;

    public FunctionToolRegistry(
        ISkillRepository skillRepo,
        IAgentSkillRepository agentSkillRepo,
        FunctionSkillRegistry executorRegistry,
        ILogger<FunctionToolRegistry> logger)
    {
        _skillRepo = skillRepo;
        _agentSkillRepo = agentSkillRepo;
        _executorRegistry = executorRegistry;
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
    /// 执行指定的 FunctionTool 技能。
    /// V2.0: 通过 FunctionSkillRegistry 解析已注册的 IFunctionSkill 执行器，
    /// 若未注册则返回错误提示，不再执行模拟/模板替换。
    /// </summary>
    public async Task<string> ExecuteAsync(string skillName, string arguments, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing FunctionTool: {SkillName}", skillName);

        // 1. 从注册表查找执行器
        var executor = _executorRegistry.Get(skillName);
        if (executor is not null)
        {
            _logger.LogDebug("Found registered executor for FunctionTool {Name}", skillName);
            return await executor.ExecuteAsync(arguments, ct);
        }

        // 2. 尝试从数据库加载 Skill 定义（仅用于日志记录）
        var skills = await _skillRepo.GetAllAsync(ct);
        var skill = skills.FirstOrDefault(s => s.Name == skillName && s.Type == SkillType.FunctionTool);
        if (skill is not null)
        {
            _logger.LogWarning(
                "FunctionTool {Name} exists in DB but has no registered executor. " +
                "Register an IFunctionSkill via FunctionSkillRegistry.Register(). " +
                "Implementation value: {Impl}",
                skillName, skill.Implementation);
        }
        else
        {
            _logger.LogWarning("FunctionTool {Name} not found in database", skillName);
        }

        return JsonSerializer.Serialize(new
        {
            error = $"FunctionTool '{skillName}' has no registered executor. " +
                    "请通过代码注册 IFunctionSkill 实现，或在技能管理中选择已注册的执行器。"
        });
    }

    /// <summary>
    /// 将 FunctionTool Skill 列表转换为 MAF AIFunction 列表。
    /// 使用 AIFunctionFactory.Create 自动从委托生成 JSON Schema。
    /// 若技能名称有对应的已注册执行器，优先使用其描述。
    /// </summary>
    public async Task<List<AIFunction>> GetAIFunctionsForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var skills = await GetFunctionToolsForAgentAsync(agentId, ct);

        return skills.Select(s =>
        {
            var executor = _executorRegistry.Get(s.Name);

            return AIFunctionFactory.Create(
                method: (string arguments, CancellationToken ct2) => ExecuteAsync(s.Name, arguments, ct2),
                name: s.Name,
                description: executor?.Description ?? s.Description
            );
        }).ToList();
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
