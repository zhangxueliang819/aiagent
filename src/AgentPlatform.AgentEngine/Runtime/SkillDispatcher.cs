using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// 技能分派器：根据 Skill.Type 找到对应的 ISkillExecutor 并执行。
///
/// 兼容新旧 SkillType 枚举：
///   - FunctionTool (旧 Tool/Api) → ToolSkillExecutor / ApiSkillExecutor
///   - AgentSkill (旧 Script/Composite) → ScriptSkillExecutor / CompositeSkillExecutor
///   - McpTool → 不经过本分派器（由 MAF LocalMcpTools 处理）
/// </summary>
public class SkillDispatcher
{
    private readonly ILogger<SkillDispatcher> _logger;
    private readonly List<(SkillType Type, ISkillExecutor Executor)> _executors;

    public SkillDispatcher(ILogger<SkillDispatcher> logger, IEnumerable<ISkillExecutor> executors)
    {
        _logger = logger;
        _executors = new List<(SkillType Type, ISkillExecutor Executor)>();

        foreach (var executor in executors)
        {
            // 使用旧的 SkillType 值映射（与 executor 一一对应）
            var type = executor switch
            {
                Skills.ToolSkillExecutor => SkillType.Tool,       // = FunctionTool (0)
                Skills.ApiSkillExecutor => SkillType.FunctionTool, // Api → 合并入 FunctionTool
                Skills.ScriptSkillExecutor => SkillType.Script,    // = AgentSkill (1)
                Skills.CompositeSkillExecutor => SkillType.AgentSkill, // Composite → 合并入 AgentSkill
                _ => SkillType.FunctionTool
            };
            _executors.Add((type, executor));
        }

        _logger.LogInformation("SkillDispatcher initialized with {Count} executors: {Types}",
            _executors.Count, string.Join(", ", _executors.Select(e => $"{e.Executor.Name}(→{e.Type})")));
    }

    /// <summary>
    /// 根据技能类型查找匹配的 Executor（优先精确匹配，其次同族匹配）
    /// </summary>
    private ISkillExecutor? FindExecutor(SkillType type)
    {
        // 1. 精确匹配
        var match = _executors.FirstOrDefault(e => e.Type == type).Executor;
        if (match is not null) return match;

        // 2. 同族映射：FunctionTool 族（Tool, Api, FunctionTool）用 ToolSkillExecutor
        if (type == SkillType.FunctionTool || type == SkillType.Tool || type == SkillType.Api)
            return _executors.FirstOrDefault(e => e.Type == SkillType.Tool).Executor
                ?? _executors.FirstOrDefault(e => e.Type == SkillType.FunctionTool).Executor;

        // 3. 同族映射：AgentSkill 族（Script, Composite, AgentSkill）用 ScriptSkillExecutor
        if (type == SkillType.AgentSkill || type == SkillType.Script || type == SkillType.Composite)
            return _executors.FirstOrDefault(e => e.Type == SkillType.Script).Executor
                ?? _executors.FirstOrDefault(e => e.Type == SkillType.AgentSkill).Executor;

        return null;
    }

    /// <summary>
    /// 根据技能定义执行
    /// </summary>
    public async Task<SkillResult> ExecuteSkillAsync(Skill skill, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        var executor = FindExecutor(skill.Type);
        if (executor is not null)
        {
            _logger.LogInformation("Dispatching skill {SkillName}({Type}) to {Executor}",
                skill.Name, skill.Type, executor.Name);

            var context = new SkillContext
            {
                Parameters = parameters,
                UserId = null
            };

            return await executor.ExecuteAsync(context, ct);
        }

        _logger.LogWarning("No executor found for skill type {Type}", skill.Type);
        return new SkillResult { Success = false, Error = $"Unsupported skill type: {skill.Type}" };
    }

    /// <summary>
    /// 直接按类型执行（用于 MCP 工具的兜底）
    /// </summary>
    public async Task<SkillResult> ExecuteByTypeAsync(SkillType type, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        var executor = FindExecutor(type);
        if (executor is not null)
            return await executor.ExecuteAsync(new SkillContext { Parameters = parameters }, ct);

        // 降级：使用 Tool executor 作为默认
        var fallback = FindExecutor(SkillType.FunctionTool);
        if (fallback is not null)
            return await fallback.ExecuteAsync(new SkillContext { Parameters = parameters }, ct);

        return new SkillResult { Success = false, Error = "No executor available" };
    }
}
