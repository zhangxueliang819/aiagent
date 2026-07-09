using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Runtime;

/// <summary>
/// 技能分派器：根据 Skill.Type 找到对应的 ISkillExecutor 并执行
/// </summary>
public class SkillDispatcher
{
    private readonly ILogger<SkillDispatcher> _logger;
    private readonly Dictionary<SkillType, ISkillExecutor> _executors;

    public SkillDispatcher(ILogger<SkillDispatcher> logger, IEnumerable<ISkillExecutor> executors)
    {
        _logger = logger;
        _executors = new Dictionary<SkillType, ISkillExecutor>();

        foreach (var executor in executors)
        {
            var type = executor switch
            {
                Skills.ToolSkillExecutor => SkillType.Tool,
                Skills.ApiSkillExecutor => SkillType.Api,
                Skills.ScriptSkillExecutor => SkillType.Script,
                Skills.CompositeSkillExecutor => SkillType.Composite,
                _ => SkillType.Tool
            };
            _executors[type] = executor;
        }

        _logger.LogInformation("SkillDispatcher initialized with {Count} executors: {Types}",
            _executors.Count, string.Join(", ", _executors.Keys));
    }

    /// <summary>
    /// 根据技能定义执行
    /// </summary>
    public async Task<SkillResult> ExecuteSkillAsync(Skill skill, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        if (_executors.TryGetValue(skill.Type, out var executor))
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
    /// 直接按函数名执行（用于 MCP 工具的兜底）
    /// </summary>
    public async Task<SkillResult> ExecuteByTypeAsync(SkillType type, Dictionary<string, object?> parameters, CancellationToken ct)
    {
        if (_executors.TryGetValue(type, out var executor))
        {
            return await executor.ExecuteAsync(new SkillContext { Parameters = parameters }, ct);
        }

        // 降级：使用 Tool executor 作为默认
        if (_executors.TryGetValue(SkillType.Tool, out var fallback))
        {
            return await fallback.ExecuteAsync(new SkillContext { Parameters = parameters }, ct);
        }

        return new SkillResult { Success = false, Error = "No executor available" };
    }
}
