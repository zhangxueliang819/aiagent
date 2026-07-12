using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

/// <summary>
/// Agent 绑定管理：给 Agent 挂载 Skill
/// </summary>
[ApiController]
[Route("api/v1/agents/{agentId:guid}/bindings")]
public class AgentBindingsController : ControllerBase
{
    private readonly IAgentSkillRepository _skillBindingRepo;
    private readonly ISkillRepository _skillRepo;

    public AgentBindingsController(
        IAgentSkillRepository skillBindingRepo,
        ISkillRepository skillRepo)
    {
        _skillBindingRepo = skillBindingRepo;
        _skillRepo = skillRepo;
    }

    // === Skill Bindings ===

    [HttpGet("skills")]
    public async Task<ActionResult<ApiResponse<List<AgentBindingDto>>>> GetSkills(Guid agentId, CancellationToken ct)
    {
        var bindings = await _skillBindingRepo.GetByAgentIdAsync(agentId, ct);
        var dtos = bindings.Select(b => new AgentBindingDto(b.Id, b.SkillId, b.Skill?.Name ?? "", b.Priority, b.IsEnabled)).ToList();
        return Ok(new ApiResponse<List<AgentBindingDto>>(true, "OK", dtos));
    }

    [HttpPost("skills")]
    public async Task<ActionResult<ApiResponse<AgentBindingDto>>> BindSkill(
        Guid agentId, [FromBody] BindRequest request, CancellationToken ct)
    {
        var skill = await _skillRepo.GetByIdAsync(request.TargetId, ct);
        if (skill is null) return NotFound(new ApiResponse<AgentBindingDto>(false, "Skill not found", null));

        var binding = await _skillBindingRepo.AddAsync(new AgentSkill
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            SkillId = request.TargetId,
            Priority = request.Priority,
            IsEnabled = true
        }, ct);

        return Ok(new ApiResponse<AgentBindingDto>(true, "Bound", new AgentBindingDto(binding.Id, binding.SkillId, skill.Name, binding.Priority, binding.IsEnabled)));
    }

    [HttpDelete("skills/{bindingId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> UnbindSkill(Guid agentId, Guid bindingId, CancellationToken ct)
    {
        await _skillBindingRepo.DeleteAsync(bindingId, ct);
        return Ok(new ApiResponse<object>(true, "Unbound", null));
    }
}

public record BindRequest(Guid TargetId, int Priority = 0);
public record AgentBindingDto(Guid BindingId, Guid TargetId, string TargetName, int Priority, bool IsEnabled);
