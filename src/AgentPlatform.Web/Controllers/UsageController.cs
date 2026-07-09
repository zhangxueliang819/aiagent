using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsageController : ControllerBase
{
    private readonly UsageService _service;

    public UsageController(UsageService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UsageRecordDto>>>> GetByUser(
        [FromQuery] string userId = "anonymous",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var records = await _service.GetByUserIdAsync(userId, fromDate, toDate, ct);
        return Ok(new ApiResponse<List<UsageRecordDto>>(true, "OK", records));
    }

    /// <summary>获取用量汇总</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<UsageSummaryDto>>> GetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var summary = await _service.GetSummaryAsync(fromDate, toDate, ct);
        return Ok(new ApiResponse<UsageSummaryDto>(true, "OK", summary));
    }

    /// <summary>获取日用量（ECharts 图表用）</summary>
    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<List<UsageDailyDto>>>> GetDaily(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var daily = await _service.GetDailyUsageAsync(fromDate, toDate, ct);
        return Ok(new ApiResponse<List<UsageDailyDto>>(true, "OK", daily));
    }

    /// <summary>获取 Agent 用量排行</summary>
    [HttpGet("agents")]
    public async Task<ActionResult<ApiResponse<List<UsageAgentSummaryDto>>>> GetAgentSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var agents = await _service.GetAgentUsageSummaryAsync(fromDate, toDate, ct);
        return Ok(new ApiResponse<List<UsageAgentSummaryDto>>(true, "OK", agents));
    }

    /// <summary>获取指定 Agent 的用量记录</summary>
    [HttpGet("agents/{agentId:guid}")]
    public async Task<ActionResult<ApiResponse<List<UsageRecordDto>>>> GetByAgent(
        Guid agentId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;
        var records = await _service.GetByAgentIdAsync(agentId, fromDate, toDate, ct);
        return Ok(new ApiResponse<List<UsageRecordDto>>(true, "OK", records));
    }
}
