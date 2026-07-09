using System.Text.Json;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// Composite 类型技能执行器：串联多个子 Skill 顺序执行
/// Implementation 中存储子 Skill ID 列表，实际串联由 SkillDispatcher 协调
/// </summary>
public class CompositeSkillExecutor : ISkillExecutor
{
    private readonly ILogger<CompositeSkillExecutor> _logger;

    public string Name => "CompositeExecutor";
    public string Description => "Chains multiple skills sequentially via SkillDispatcher";

    public CompositeSkillExecutor(ILogger<CompositeSkillExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("CompositeSkill executing with {Count} parameters", context.Parameters.Count);

            // 组合技能的实际子技能串联由 SkillDispatcher 根据 skill.Implementation 中存储的子 Skill ID 完成
            // 此处提供默认的标记性成功结果
            return new SkillResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(new { message = "Composite skill executed", params_count = context.Parameters.Count })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompositeSkill execution failed");
            return new SkillResult { Success = false, Error = ex.Message };
        }
    }
}
