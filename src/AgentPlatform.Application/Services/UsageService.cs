using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class UsageService
{
    private readonly IUsageRepository _repository;

    public UsageService(IUsageRepository repository) => _repository = repository;

    public async Task<List<UsageRecordDto>> GetByUserIdAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _repository.GetByUserIdAsync(userId, from, to, ct);
        return records.Select(r => new UsageRecordDto(r.Id, r.ModelId, r.InputTokens, r.OutputTokens, r.Cost, r.CreatedAt)).ToList();
    }
}
