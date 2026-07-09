using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class AgentService
{
    private readonly IAgentRepository _repository;

    public AgentService(IAgentRepository repository) => _repository = repository;

    public async Task<List<AgentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var agents = await _repository.GetAllAsync(ct);
        return agents.Select(Map).ToList();
    }

    public async Task<AgentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var agent = await _repository.GetByIdAsync(id, ct);
        return agent is null ? null : Map(agent);
    }

    public async Task<AgentDto> CreateAsync(CreateAgentRequest request, CancellationToken ct = default)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            SystemPrompt = request.SystemPrompt,
            ModelId = request.ModelId,
            ModelEndpointId = request.ModelEndpointId,
            CreatedBy = request.CreatedBy,
            Status = AgentStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var result = await _repository.AddAsync(agent, ct);
        return Map(result);
    }

    public async Task<AgentDto?> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken ct = default)
    {
        var agent = await _repository.GetByIdAsync(id, ct);
        if (agent is null) return null;

        if (request.Name is not null) agent.Name = request.Name;
        if (request.Description is not null) agent.Description = request.Description;
        if (request.SystemPrompt is not null) agent.SystemPrompt = request.SystemPrompt;
        if (request.ModelId is not null) agent.ModelId = request.ModelId;
        if (request.ModelEndpointId.HasValue) agent.ModelEndpointId = request.ModelEndpointId;
        if (request.Status is not null && Enum.TryParse<AgentStatus>(request.Status, out var s))
            agent.Status = s;
        agent.UpdatedAt = DateTime.UtcNow;

        var result = await _repository.UpdateAsync(agent, ct);
        return Map(result);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return true;
    }

    private static AgentDto Map(Agent a) => new(
        a.Id, a.Name, a.Description, a.SystemPrompt,
        a.ModelId, a.ModelEndpointId, a.ModelEndpoint?.ModelName,
        a.Status.ToString(), a.Version,
        a.CreatedBy, a.CreatedAt, a.UpdatedAt,
        a.Configurations.Select(c => new AgentConfigurationDto(c.Id, c.Key, c.Value, c.ValueType)).ToList(),
        a.Skills.Select(s => new AgentSkillDto(s.Id, s.SkillId, s.Priority, s.IsEnabled)).ToList());
}
