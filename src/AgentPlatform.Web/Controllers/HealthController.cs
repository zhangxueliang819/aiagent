using AgentPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AgentPlatformDbContext _db;
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public HealthController(AgentPlatformDbContext db) => _db = db;

    /// <summary>基础健康检查</summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            uptime = (DateTime.UtcNow - StartTime).TotalSeconds.ToString("F0") + "s"
        });
    }

    /// <summary>存活探针（Liveness）：应用是否在运行</summary>
    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    /// <summary>就绪探针（Readiness）：依赖服务是否可用</summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var checks = new Dictionary<string, object>
        {
            ["database"] = await CheckDatabaseAsync(),
            ["uptime"] = (DateTime.UtcNow - StartTime).TotalSeconds.ToString("F0") + "s"
        };

        var allHealthy = checks.Values.All(v => v is string s ? s == "healthy" : true);
        return allHealthy
            ? Ok(new { status = "ready", checks })
            : StatusCode(503, new { status = "not ready", checks });
    }

    /// <summary>基础指标</summary>
    [HttpGet("metrics")]
    public IActionResult Metrics()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        return Ok(new
        {
            uptimeSeconds = (DateTime.UtcNow - StartTime).TotalSeconds.ToString("F0"),
            memoryMb = (process.WorkingSet64 / 1024 / 1024).ToString("F0"),
            cpuTimeSeconds = process.TotalProcessorTime.TotalSeconds.ToString("F1"),
            threadCount = process.Threads.Count,
            timestamp = DateTime.UtcNow
        });
    }

    private async Task<string> CheckDatabaseAsync()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            return canConnect ? "healthy" : "unhealthy";
        }
        catch
        {
            return "unhealthy";
        }
    }
}
