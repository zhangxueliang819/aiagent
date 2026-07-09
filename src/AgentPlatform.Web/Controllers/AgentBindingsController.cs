using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

/// <summary>
/// Agent 绑定管理：给 Agent 挂载 Skill 和 MCP 端点
/// </summary>
[ApiController]
[Route("api/v1/agents/{agentId:guid}/bindings")]
public class AgentBindingsController : ControllerBase
{
    private readonly IAgentSkillRepository _skillBindingRepo;
    private readonly IAgentMcpEndpointRepository _mcpBindingRepo;
    private readonly ISkillRepository _skillRepo;
    private readonly IMcpEndpointRepository _mcpRepo;

    public AgentBindingsController(
        IAgentSkillRepository skillBindingRepo,
        IAgentMcpEndpointRepository mcpBindingRepo,
        ISkillRepository skillRepo,
        IMcpEndpointRepository mcpRepo)
    {
        _skillBindingRepo = skillBindingRepo;
        _mcpBindingRepo = mcpBindingRepo;
        _skillRepo = skillRepo;
        _mcpRepo = mcpRepo;
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

    // === MCP Bindings ===

    [HttpGet("mcp")]
    public async Task<ActionResult<ApiResponse<List<AgentBindingDto>>>> GetMcpEndpoints(Guid agentId, CancellationToken ct)
    {
        var bindings = await _mcpBindingRepo.GetByAgentIdAsync(agentId, ct);
        var dtos = bindings.Select(b => new AgentBindingDto(b.Id, b.McpEndpointId, b.McpEndpoint?.Name ?? "", b.Priority, b.IsEnabled)).ToList();
        return Ok(new ApiResponse<List<AgentBindingDto>>(true, "OK", dtos));
    }

    [HttpPost("mcp")]
    public async Task<ActionResult<ApiResponse<AgentBindingDto>>> BindMcp(
        Guid agentId, [FromBody] BindRequest request, CancellationToken ct)
    {
        var mcp = await _mcpRepo.GetByIdWithToolsAsync(request.TargetId, ct);
        if (mcp is null) return NotFound(new ApiResponse<AgentBindingDto>(false, "MCP Endpoint not found", null));

        var binding = await _mcpBindingRepo.AddAsync(new AgentMcpEndpoint
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            McpEndpointId = request.TargetId,
            Priority = request.Priority,
            IsEnabled = true
        }, ct);

        return Ok(new ApiResponse<AgentBindingDto>(true, "Bound", new AgentBindingDto(binding.Id, binding.McpEndpointId, mcp.Name, binding.Priority, binding.IsEnabled)));
    }

    [HttpDelete("mcp/{bindingId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> UnbindMcp(Guid agentId, Guid bindingId, CancellationToken ct)
    {
        await _mcpBindingRepo.DeleteAsync(bindingId, ct);
        return Ok(new ApiResponse<object>(true, "Unbound", null));
    }
}

public record BindRequest(Guid TargetId, int Priority = 0);
public record AgentBindingDto(Guid BindingId, Guid TargetId, string TargetName, int Priority, bool IsEnabled);
