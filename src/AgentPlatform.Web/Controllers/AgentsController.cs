using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AgentService _service;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(AgentService service, ILogger<AgentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AgentDto>>>> GetAll(CancellationToken ct)
    {
        var agents = await _service.GetAllAsync(ct);
        return Ok(new ApiResponse<List<AgentDto>>(true, "OK", agents));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AgentDto>>> GetById(Guid id, CancellationToken ct)
    {
        var agent = await _service.GetByIdAsync(id, ct);
        if (agent is null)
            return NotFound(new ApiResponse<AgentDto>(false, "Agent not found", null));
        return Ok(new ApiResponse<AgentDto>(true, "OK", agent));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AgentDto>>> Create([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var agent = await _service.CreateAsync(request, ct);
        _logger.LogInformation("Agent created: {AgentId} - {Name}", agent.Id, agent.Name);
        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, new ApiResponse<AgentDto>(true, "Created", agent));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AgentDto>>> Update(Guid id, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var agent = await _service.UpdateAsync(id, request, ct);
        if (agent is null)
            return NotFound(new ApiResponse<AgentDto>(false, "Agent not found", null));
        return Ok(new ApiResponse<AgentDto>(true, "Updated", agent));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }
}
