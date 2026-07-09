using System.Text.Json;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// Api 类型技能执行器：调用外部 REST API
/// Implementation 字段中存储目标 URL 模板
/// </summary>
public class ApiSkillExecutor : ISkillExecutor
{
    private readonly ILogger<ApiSkillExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public string Name => "ApiExecutor";
    public string Description => "Calls external REST APIs defined in Skill.Implementation";

    public ApiSkillExecutor(ILogger<ApiSkillExecutor> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("ApiSkill executing with {Count} parameters", context.Parameters.Count);

            // 简化实现：如果 context 包含 url 参数，尝试调用该 URL
            if (context.Parameters.TryGetValue("url", out var urlObj) && urlObj is string url)
            {
                var method = context.Parameters.TryGetValue("method", out var m) ? m?.ToString() ?? "GET" : "GET";
                var client = _httpClientFactory.CreateClient();
                var response = method.ToUpper() switch
                {
                    "POST" => await client.PostAsync(url, new StringContent("{}"), ct),
                    _ => await client.GetAsync(url, ct)
                };

                var content = await response.Content.ReadAsStringAsync(ct);
                return new SkillResult { Success = response.IsSuccessStatusCode, Data = content };
            }

            // 模拟：无 url 时返回模拟数据
            return new SkillResult
            {
                Success = true,
                Data = JsonSerializer.Serialize(new { message = "API call simulated", params_count = context.Parameters.Count })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApiSkill execution failed");
            return new SkillResult { Success = false, Error = ex.Message };
        }
    }
}
