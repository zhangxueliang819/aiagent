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
        return records.Select(Map).ToList();
    }

    public async Task<List<UsageRecordDto>> GetByAgentIdAsync(Guid agentId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _repository.GetByAgentIdAsync(agentId, from, to, ct);
        return records.Select(Map).ToList();
    }

    public async Task<UsageSummaryDto> GetSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _repository.GetAllAsync(from, to, ct);

        return new UsageSummaryDto(
            TotalRequests: records.Count,
            TotalInputTokens: records.Sum(r => r.InputTokens),
            TotalOutputTokens: records.Sum(r => r.OutputTokens),
            TotalCost: records.Sum(r => r.Cost),
            AgentCount: records.Where(r => r.AgentId.HasValue).Select(r => r.AgentId!.Value).Distinct().Count()
        );
    }

    public async Task<List<UsageDailyDto>> GetDailyUsageAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _repository.GetAllAsync(from, to, ct);

        return records
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new UsageDailyDto(
                Date: g.Key.ToString("yyyy-MM-dd"),
                RequestCount: g.Count(),
                InputTokens: g.Sum(r => r.InputTokens),
                OutputTokens: g.Sum(r => r.OutputTokens),
                Cost: g.Sum(r => r.Cost)
            ))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<List<UsageAgentSummaryDto>> GetAgentUsageSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _repository.GetAllAsync(from, to, ct);

        return records
            .Where(r => r.AgentId.HasValue)
            .GroupBy(r => r.AgentId!.Value)
            .Select(g => new UsageAgentSummaryDto(
                AgentId: g.Key,
                RequestCount: g.Count(),
                TotalTokens: g.Sum(r => r.InputTokens + r.OutputTokens),
                TotalCost: g.Sum(r => r.Cost)
            ))
            .OrderByDescending(a => a.TotalCost)
            .ToList();
    }

    private static UsageRecordDto Map(UsageRecord r) =>
        new(r.Id, r.ModelId, r.InputTokens, r.OutputTokens, r.Cost, r.CreatedAt);
}
