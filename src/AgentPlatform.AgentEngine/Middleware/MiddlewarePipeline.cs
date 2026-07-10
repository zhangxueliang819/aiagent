using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Middleware;

/// <summary>
/// MAF Agent Middleware 接口（简化版，不依赖 MAF 包）。
/// 对应 MAF 的 Middleware/Filter 管道概念。
///
/// 用途：在 Agent 执行对话前后插入横切关注点（日志、速率限制、审计、审批等）。
/// MAF 阶段：直接使用 MAF 的 Middleware 管道。
/// </summary>
public interface IAgentMiddleware
{
    /// <summary>中间件名称</summary>
    string Name { get; }

    /// <summary>
    /// 在 Agent 处理用户消息之前执行
    /// </summary>
    /// <param name="context">中间件上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>如果返回 false 则中断管道（不再执行后续中间件和 Agent）</returns>
    Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct);

    /// <summary>
    /// 在 Agent 处理用户消息之后执行
    /// </summary>
    /// <param name="context">中间件上下文</param>
    /// <param name="response">Agent 响应</param>
    /// <param name="ct">取消令牌</param>
    Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct);

    /// <summary>
    /// 中间件执行出错时
    /// </summary>
    Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct);
}

/// <summary>
/// 中间件上下文
/// </summary>
public class AgentMiddlewareContext
{
    public Guid AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public string? UserId { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();
}

/// <summary>
/// 中间件管道执行器
/// </summary>
public class MiddlewarePipeline
{
    private readonly List<IAgentMiddleware> _middlewares;
    private readonly ILogger<MiddlewarePipeline> _logger;

    public MiddlewarePipeline(
        IEnumerable<IAgentMiddleware> middlewares,
        ILogger<MiddlewarePipeline> logger)
    {
        _middlewares = middlewares.OrderBy(m => m is LoggingMiddleware ? 0
            : m is RateLimitingMiddleware ? 1
            : m is AuditMiddleware ? 2
            : m is ToolApprovalMiddleware ? 3
            : 99).ToList();
        _logger = logger;
    }

    /// <summary>
    /// 执行 Before 管道。返回 null 表示正常继续；返回非 null 表示被断路（短路响应）。
    /// </summary>
    public async Task<object?> ExecuteBeforeAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        foreach (var mw in _middlewares)
        {
            try
            {
                var shouldContinue = await mw.OnBeforeAsync(context, ct);
                if (!shouldContinue)
                {
                    _logger.LogInformation("Middleware {Name} interrupted the pipeline", mw.Name);
                    return null; // 调用方应处理为"请求被拒绝"
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Middleware {Name} OnBefore failed", mw.Name);
                await mw.OnErrorAsync(context, ex, ct);
            }
        }
        return null; // 所有中间件通过
    }

    /// <summary>
    /// 执行 After 管道
    /// </summary>
    public async Task ExecuteAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
    {
        foreach (var mw in _middlewares)
        {
            try
            {
                await mw.OnAfterAsync(context, response, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Middleware {Name} OnAfter failed", mw.Name);
                await mw.OnErrorAsync(context, ex, ct);
            }
        }
    }
}

// ============================================================
// 内置中间件实现
// ============================================================

/// <summary>
/// 日志中间件：记录每次 Agent 调用的耗时和响应摘要
/// </summary>
public class LoggingMiddleware : IAgentMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;
    public string Name => "Logging";

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger) => _logger = logger;

    public Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        context.StartedAt = DateTime.UtcNow;
        context.Properties["StartTime"] = context.StartedAt;
        _logger.LogInformation("[MW-Logging] Agent {Name} processing: {Msg}", context.AgentName,
            context.UserMessage[..Math.Min(200, context.UserMessage.Length)]);
        return Task.FromResult(true);
    }

    public Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
    {
        var elapsed = DateTime.UtcNow - context.StartedAt;
        _logger.LogInformation("[MW-Logging] Agent {Name} completed in {Elapsed}ms", context.AgentName,
            elapsed.TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "[MW-Logging] Agent {Name} error", context.AgentName);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 速率限制中间件：基于滑动窗口限制 Agent 调用频率
/// </summary>
public class RateLimitingMiddleware : IAgentMiddleware
{
    private static readonly Dictionary<string, (int Count, DateTime WindowStart)> _counters = new();
    private static readonly object _lock = new();
    private readonly int _maxRequestsPerMinute;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    public string Name => "RateLimiting";

    public RateLimitingMiddleware(ILogger<RateLimitingMiddleware> logger, int maxRequestsPerMinute = 60)
    {
        _logger = logger;
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    public Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        var key = context.UserId ?? context.AgentId.ToString();
        lock (_lock)
        {
            if (_counters.TryGetValue(key, out var counter))
            {
                if (DateTime.UtcNow - counter.WindowStart > TimeSpan.FromMinutes(1))
                    _counters[key] = (1, DateTime.UtcNow);
                else if (counter.Count >= _maxRequestsPerMinute)
                {
                    _logger.LogWarning("[MW-RateLimit] Rate limit exceeded for {Key}", key);
                    return Task.FromResult(false);
                }
                else
                    _counters[key] = (counter.Count + 1, counter.WindowStart);
            }
            else
                _counters[key] = (1, DateTime.UtcNow);
        }
        return Task.FromResult(true);
    }

    public Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
        => Task.CompletedTask;

    public Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct)
        => Task.CompletedTask;
}

/// <summary>
/// 审计中间件：将 Agent 调用记录写入 AuditLog 表
/// </summary>
public class AuditMiddleware : IAgentMiddleware
{
    private readonly IAuditLogRepository _auditRepo;
    private readonly ILogger<AuditMiddleware> _logger;
    public string Name => "Audit";

    public AuditMiddleware(IAuditLogRepository auditRepo, ILogger<AuditMiddleware> logger)
    {
        _auditRepo = auditRepo;
        _logger = logger;
    }

    public Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct) => Task.FromResult(true);

    public async Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
    {
        await _auditRepo.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "AgentCall",
            EntityType = "Agent",
            EntityId = context.AgentId.ToString(),
            UserId = context.UserId ?? "system",
            Changes = System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = context.SessionId,
                message = context.UserMessage[..Math.Min(500, context.UserMessage.Length)],
                elapsed = (DateTime.UtcNow - context.StartedAt).TotalMilliseconds
            }),
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    public Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// 工具审批中间件：对敏感工具调用进行人工审批（Human-in-the-Loop）
/// </summary>
public class ToolApprovalMiddleware : IAgentMiddleware
{
    private readonly ILogger<ToolApprovalMiddleware> _logger;
    public string Name => "ToolApproval";

    /// <summary>需要审批的工具名称前缀</summary>
    public List<string> RequireApprovalPrefixes { get; set; } = new() { "delete_", "admin_", "sudo_" };

    public ToolApprovalMiddleware(ILogger<ToolApprovalMiddleware> logger) => _logger = logger;

    public Task<bool> OnBeforeAsync(AgentMiddlewareContext context, CancellationToken ct)
    {
        // 检查用户消息是否涉及敏感操作
        foreach (var prefix in RequireApprovalPrefixes)
        {
            if (context.UserMessage.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // 标记为需要审批
                context.Properties["RequiresApproval"] = true;
                context.Properties["ApprovalReason"] = $"消息包含敏感操作关键词: {prefix}";
                _logger.LogWarning("[MW-ToolApproval] Sensitive operation detected: {Prefix}", prefix);
            }
        }
        return Task.FromResult(true);
    }

    public Task OnAfterAsync(AgentMiddlewareContext context, object? response, CancellationToken ct)
        => Task.CompletedTask;

    public Task OnErrorAsync(AgentMiddlewareContext context, Exception ex, CancellationToken ct)
        => Task.CompletedTask;
}
