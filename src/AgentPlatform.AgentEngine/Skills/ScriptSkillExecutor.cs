using System.Text.Json;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// Script 类型技能执行器：执行脚本任务
/// 当前为模拟实现，真实场景可对接 Python/Node.js 沙箱
/// </summary>
public class ScriptSkillExecutor : ISkillExecutor
{
    private readonly ILogger<ScriptSkillExecutor> _logger;

    public string Name => "ScriptExecutor";
    public string Description => "Executes scripts defined in Skill.Implementation";

    public ScriptSkillExecutor(ILogger<ScriptSkillExecutor> logger)
    {
        _logger = logger;
    }

    public Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("ScriptSkill executing with {Count} parameters", context.Parameters.Count);

            // 模拟脚本执行结果
            var result = new Dictionary<string, object?>
            {
                ["status"] = "simulated",
                ["output"] = "Script execution simulated (sandbox not configured)",
                ["params_count"] = context.Parameters.Count
            };

            return Task.FromResult(new SkillResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(result)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScriptSkill execution failed");
            return Task.FromResult(new SkillResult { Success = false, Error = ex.Message });
        }
    }
}
