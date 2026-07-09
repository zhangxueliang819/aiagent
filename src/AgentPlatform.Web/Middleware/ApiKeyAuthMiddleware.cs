using System.Security.Claims;
using System.Text.Json;

namespace AgentPlatform.Web.Middleware;

/// <summary>
/// API Key 认证中间件
/// 检查 X-API-Key 请求头，与数据库中的 ApiKey 记录匹配。
/// 如果匹配，设置用户身份；如果不匹配或不存在，继续管道（由 JWT 或 [Authorize] 处理）。
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    // Phase A 简化版：硬编码测试 API Key（Phase B 将迁移到数据库验证）
    private const string DevApiKey = "ap_dev_agent_platform_2024";
    private const string TestApiKey = "ap_test_agent_platform_2024";

    private static readonly Dictionary<string, (string Username, string DisplayName, string Role)> ApiKeys = new()
    {
        [DevApiKey] = ("api-dev", "API 开发者", "Admin"),
        [TestApiKey] = ("api-test", "API 测试者", "User")
    };

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 如果已有身份（JWT 认证），跳过 API Key 检查
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // 检查 X-API-Key 请求头
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            var key = apiKey.ToString();
            if (ApiKeys.TryGetValue(key, out var userInfo))
            {
                _logger.LogInformation("API Key authenticated: {Username}", userInfo.Username);

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userInfo.Username),
                    new Claim(ClaimTypes.Name, userInfo.DisplayName),
                    new Claim(ClaimTypes.Role, userInfo.Role),
                    new Claim("auth_method", "api_key")
                };

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);
            }
            else
            {
                _logger.LogWarning("Invalid API Key attempt: {KeyPrefix}...", key[..Math.Min(key.Length, 8)]);
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { success = false, message = "无效的 API Key" });
                await context.Response.WriteAsync(error);
                return;
            }
        }

        await _next(context);
    }
}
