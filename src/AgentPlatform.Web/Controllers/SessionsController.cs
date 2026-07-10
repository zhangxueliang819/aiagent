using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly SessionService _service;

    public SessionsController(SessionService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SessionDto>>>> GetByUser([FromQuery] string userId, CancellationToken ct)
    {
        var sessions = await _service.GetByUserIdAsync(userId, ct);
        return Ok(new ApiResponse<List<SessionDto>>(true, "OK", sessions));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SessionDto>>> GetById(Guid id, CancellationToken ct)
    {
        var s = await _service.GetByIdAsync(id, ct);
        if (s is null) return NotFound(new ApiResponse<SessionDto>(false, "Not found", null));
        return Ok(new ApiResponse<SessionDto>(true, "OK", s));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SessionDto>>> Create([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        var s = await _service.CreateAsync(request.UserId, request.AgentId, request.Title, ct);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, new ApiResponse<SessionDto>(true, "Created", s));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return Ok(new ApiResponse<bool>(true, "Deleted", result));
    }
}

public record CreateSessionRequest(string UserId, Guid AgentId, string Title);
