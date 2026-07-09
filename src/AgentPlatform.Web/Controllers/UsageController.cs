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
        [FromQuery] string userId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct)
    {
        var records = await _service.GetByUserIdAsync(userId, from, to, ct);
        return Ok(new ApiResponse<List<UsageRecordDto>>(true, "OK", records));
    }
}
