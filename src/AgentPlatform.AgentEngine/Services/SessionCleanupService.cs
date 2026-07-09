using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Services;

/// <summary>
/// 会话超时自动回收后台服务
/// 定期检查 Active 状态的会话，超时（默认 30 分钟无更新）自动标记为 Completed
/// </summary>
public class SessionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupService> _logger;

    private const int SessionTimeoutMinutes = 30;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public SessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup service started (timeout: {Timeout}min, interval: {Interval}min)",
            SessionTimeoutMinutes, CheckInterval.TotalMinutes);

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // 启动后延迟1分钟

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStaleSessionsAsync(stoppingToken);
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task CleanupStaleSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

        var threshold = DateTime.UtcNow.AddMinutes(-SessionTimeoutMinutes);
        var staleSessions = await sessionRepo.GetActiveSessionsOlderThanAsync(threshold, ct);

        foreach (var session in staleSessions)
        {
            session.Status = SessionStatus.Completed;
            session.UpdatedAt = DateTime.UtcNow;
            await sessionRepo.UpdateAsync(session, ct);
            _logger.LogInformation("Session {SessionId} auto-completed due to inactivity", session.Id);
        }

        if (staleSessions.Count > 0)
        {
            _logger.LogInformation("Session cleanup: auto-completed {Count} stale sessions", staleSessions.Count);
        }
    }
}
