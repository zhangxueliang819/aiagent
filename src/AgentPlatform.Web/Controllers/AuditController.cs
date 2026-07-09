using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using AgentPlatform.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/audit")]
public class AuditController : ControllerBase
{
    private readonly AuditService _service;

    public AuditController(AuditService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AuditLog>>>> GetAll(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
        var toDate = to ?? DateTime.UtcNow;
        var logs = await _service.GetAllAsync(fromDate, toDate, ct);
        return Ok(new ApiResponse<List<AuditLog>>(true, "OK", logs));
    }
}

[ApiController]
[Route("api/v1/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _service;

    public UsersController(UserService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<User>>>> GetAll(CancellationToken ct)
    {
        var users = await _service.GetAllAsync(ct);
        return Ok(new ApiResponse<List<User>>(true, "OK", users));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<User>>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _service.GetByIdAsync(id, ct);
        if (user is null) return NotFound(new ApiResponse<User>(false, "Not found", null));
        return Ok(new ApiResponse<User>(true, "OK", user));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<User>>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try
        {
            var user = await _service.CreateAsync(request.Username, request.Password,
                request.DisplayName, request.Email, request.Role ?? "user", ct);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new ApiResponse<User>(true, "Created", user));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<User>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<User>>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _service.UpdateAsync(id, request.DisplayName, request.Email, request.Role, request.IsEnabled, ct);
        if (user is null) return NotFound(new ApiResponse<User>(false, "Not found", null));
        return Ok(new ApiResponse<User>(true, "Updated", user));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }
}

public record CreateUserRequest(string Username, string Password, string DisplayName, string Email, string? Role);
public record UpdateUserRequest(string? DisplayName, string? Email, string? Role, bool? IsEnabled);
