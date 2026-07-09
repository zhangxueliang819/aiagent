using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class SkillService
{
    private readonly ISkillRepository _repository;

    public SkillService(ISkillRepository repository) => _repository = repository;

    public async Task<List<SkillDto>> GetAllAsync(CancellationToken ct = default)
    {
        var skills = await _repository.GetAllAsync(ct);
        return skills.Select(Map).ToList();
    }

    public async Task<SkillDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _repository.GetByIdAsync(id, ct);
        return s is null ? null : Map(s);
    }

    public async Task<SkillDto> CreateAsync(CreateSkillRequest request, CancellationToken ct = default)
    {
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Type = Enum.TryParse<SkillType>(request.Type, out var t) ? t : SkillType.Tool,
            Implementation = request.Implementation,
            InputSchema = request.InputSchema,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Map(await _repository.AddAsync(skill, ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return true;
    }

    private static SkillDto Map(Skill s) => new(
        s.Id, s.Name, s.Description, s.Type.ToString(),
        s.InputSchema, s.IsEnabled, s.CreatedAt);
}
