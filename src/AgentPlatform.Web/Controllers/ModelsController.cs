using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly ModelProviderService _service;
    private readonly ModelRouter _router;
    private readonly ModelMetricsCollector _metrics;

    public ModelsController(ModelProviderService service, ModelRouter router, ModelMetricsCollector metrics)
    {
        _service = service;
        _router = router;
        _metrics = metrics;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ModelProviderDto>>>> GetAll(CancellationToken ct)
    {
        var providers = await _service.GetAllAsync(ct);
        return Ok(new ApiResponse<List<ModelProviderDto>>(true, "OK", providers));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ModelProviderDto>>> GetById(Guid id, CancellationToken ct)
    {
        var p = await _service.GetByIdAsync(id, ct);
        if (p is null) return NotFound(new ApiResponse<ModelProviderDto>(false, "Not found", null));
        return Ok(new ApiResponse<ModelProviderDto>(true, "OK", p));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ModelProviderDto>>> Create([FromBody] CreateModelProviderRequest request, CancellationToken ct)
    {
        var p = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = p.Id }, new ApiResponse<ModelProviderDto>(true, "Created", p));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ModelProviderDto>>> Update(Guid id, [FromBody] CreateModelProviderRequest request, CancellationToken ct)
    {
        var p = await _service.UpdateAsync(id, request, ct);
        if (p is null) return NotFound(new ApiResponse<ModelProviderDto>(false, "Not found", null));
        return Ok(new ApiResponse<ModelProviderDto>(true, "Updated", p));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }

    // ─── Endpoint Management ────────────────────────────────────────

    /// <summary>为指定 Provider 添加模型端点</summary>
    [HttpPost("{providerId:guid}/endpoints")]
    public async Task<ActionResult<ApiResponse<ModelEndpointDto>>> AddEndpoint(
        Guid providerId, [FromBody] CreateModelEndpointRequest request, CancellationToken ct)
    {
        var ep = await _service.AddEndpointAsync(providerId, request, ct);
        return Ok(new ApiResponse<ModelEndpointDto>(true, "Created", ep));
    }

    /// <summary>删除模型端点</summary>
    [HttpDelete("{providerId:guid}/endpoints/{endpointId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteEndpoint(
        Guid providerId, Guid endpointId, CancellationToken ct)
    {
        await _service.DeleteEndpointAsync(endpointId, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }

    // ─── Metrics ────────────────────────────────────────────────

    /// <summary>获取所有模型端点的性能指标</summary>
    [HttpGet("metrics")]
    public ActionResult<ApiResponse<List<ModelMetricsDto>>> GetMetrics()
    {
        var allMetrics = _metrics.GetAllMetrics();
        var dtos = allMetrics.Select(m => new ModelMetricsDto(
            m.EndpointId.ToString(), "", m.ActiveRequests, m.TotalRequests,
            m.ErrorCount, m.AvgLatencyMs, false)).ToList();
        return Ok(new ApiResponse<List<ModelMetricsDto>>(true, "OK", dtos));
    }

    /// <summary>清除指定端点的缓存和指标</summary>
    [HttpPost("cache/{endpointId:guid}/refresh")]
    public ActionResult<ApiResponse<object>> RefreshEndpointCache(Guid endpointId)
    {
        _router.InvalidateCache(endpointId);
        return Ok(new ApiResponse<object>(true, "Cache cleared", null));
    }
}
