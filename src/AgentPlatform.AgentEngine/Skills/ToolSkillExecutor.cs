using System.Text.Json;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// Tool 类型技能执行器：模拟调用外部工具函数
/// 从 Implementation 字段中读取目标 URL/Payload，返回 JSON 结果
/// </summary>
public class ToolSkillExecutor : ISkillExecutor
{
    private readonly ILogger<ToolSkillExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public string Name => "ToolExecutor";
    public string Description => "Executes external tool/API calls defined in Skill.Implementation";

    public ToolSkillExecutor(ILogger<ToolSkillExecutor> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct)
    {
        try
        {
            var inputJson = JsonSerializer.Serialize(context.Parameters);
            _logger.LogInformation("ToolSkill executing with params: {Params}", inputJson);

            // 模拟：实际应从 context 中读取 implementation 配置的 URL
            // 这里返回成功的模拟结果
            var result = new Dictionary<string, object?>
            {
                ["status"] = "ok",
                ["input"] = context.Parameters,
                ["output"] = $"Tool executed with {context.Parameters.Count} parameters"
            };

            return new SkillResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(result)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ToolSkill execution failed");
            return new SkillResult { Success = false, Error = ex.Message };
        }
    }
}
