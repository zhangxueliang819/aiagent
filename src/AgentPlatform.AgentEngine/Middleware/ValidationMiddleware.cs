using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Middleware;

/// <summary>
/// 验证中间件 — 在 Agent 处理请求前对用户输入进行验证。
///
/// 用途：
/// - 输入长度限制
/// - 敏感内容检测
/// - 输入格式校验
/// - 权限验证
///
/// 设计依据（Microsoft Agent Framework 文档 — 代理中间件）：
/// - 中间件会截获对代理的运行方法的每个调用
/// - 可用于记录、验证或转换
/// - OnBefore 返回 false 可中断管道
/// </summary>
public class ValidationMiddleware : IAgentMiddleware
{
    private readonly IReadOnlyList<Func<AgentMiddlewareContext, ValueTask<bool>>> _validators;
    private readonly ILogger<ValidationMiddleware> _logger;

    public string Name => "Validation";

    /// <summary>验证失败时的错误消息</summary>
    public string RejectionMessage { get; set; } = "请求未通过验证";

    public ValidationMiddleware(
        IReadOnlyList<Func<AgentMiddlewareContext, ValueTask<bool>>> validators,
        ILogger<ValidationMiddleware> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public ValidationMiddleware(
        Func<AgentMiddlewareContext, ValueTask<bool>> validator,
        ILogger<ValidationMiddleware> logger)
        : this(new[] { validator }, logger)
    {
    }

    public async Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        foreach (var validator in _validators)
        {
            try
            {
                var passed = await validator(context);
                if (!passed)
                {
                    _logger.LogWarning(
                        "[MW-Validation] Validation failed for agent {AgentName}: {Message}",
                        context.AgentName, RejectionMessage);

                    context.Properties["ValidationError"] = RejectionMessage;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MW-Validation] Validator threw exception for agent {AgentName}",
                    context.AgentName);
                return false;
            }
        }

        return true;
    }

    public Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
        => Task.CompletedTask;

    public Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct)
        => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════
    //  内置验证器工厂方法
    // ══════════════════════════════════════════════════════════════

    /// <summary>输入最大长度验证</summary>
    public static Func<AgentMiddlewareContext, ValueTask<bool>> MaxLength(int maxChars)
    {
        return ctx => ValueTask.FromResult(ctx.UserMessage.Length <= maxChars);
    }

    /// <summary>输入最小长度验证</summary>
    public static Func<AgentMiddlewareContext, ValueTask<bool>> MinLength(int minChars)
    {
        return ctx => ValueTask.FromResult(ctx.UserMessage.Length >= minChars);
    }

    /// <summary>敏感内容屏蔽验证</summary>
    public static Func<AgentMiddlewareContext, ValueTask<bool>> NoSensitiveContent(
        IEnumerable<string> blockedKeywords, ILogger? logger = null)
    {
        var keywords = blockedKeywords.Select(k => k.ToLowerInvariant()).ToHashSet();
        return ctx =>
        {
            var lower = ctx.UserMessage.ToLowerInvariant();
            var found = keywords.FirstOrDefault(k => lower.Contains(k));
            if (found is not null)
            {
                logger?.LogWarning("Sensitive content detected: '{Keyword}' in message", found);
                return ValueTask.FromResult(false);
            }
            return ValueTask.FromResult(true);
        };
    }

    /// <summary>空消息验证</summary>
    public static Func<AgentMiddlewareContext, ValueTask<bool>> NotEmpty()
    {
        return ctx => ValueTask.FromResult(!string.IsNullOrWhiteSpace(ctx.UserMessage));
    }
}
