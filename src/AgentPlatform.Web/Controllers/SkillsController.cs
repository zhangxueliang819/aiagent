using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SkillsController : ControllerBase
{
    private readonly SkillService _service;

    public SkillsController(SkillService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SkillDto>>>> GetAll(CancellationToken ct)
    {
        var skills = await _service.GetAllAsync(ct);
        return Ok(new ApiResponse<List<SkillDto>>(true, "OK", skills));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillDto>>> GetById(Guid id, CancellationToken ct)
    {
        var s = await _service.GetByIdAsync(id, ct);
        if (s is null) return NotFound(new ApiResponse<SkillDto>(false, "Not found", null));
        return Ok(new ApiResponse<SkillDto>(true, "OK", s));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SkillDto>>> Create([FromBody] CreateSkillRequest request, CancellationToken ct)
    {
        var s = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, new ApiResponse<SkillDto>(true, "Created", s));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(new ApiResponse<object>(true, "Deleted", null));
    }
}
