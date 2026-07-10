using System.Text.Json;
using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class AgentService
{
    private readonly IAgentRepository _repository;
    private readonly IAgentVersionRepository _versionRepo;

    public AgentService(IAgentRepository repository, IAgentVersionRepository versionRepo)
    {
        _repository = repository;
        _versionRepo = versionRepo;
    }

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
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            CreatedBy = request.CreatedBy,
            Status = AgentStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var result = await _repository.AddAsync(agent, ct);

        // 创建初始版本
        await CreateVersionSnapshotAsync(agent, "初始创建", request.CreatedBy, ct);

        return Map(result);
    }

    public async Task<AgentDto?> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken ct = default)
    {
        var agent = await _repository.GetByIdAsync(id, ct);
        if (agent is null) return null;

        var changes = new List<string>();

        if (request.Name is not null) { agent.Name = request.Name; changes.Add("名称"); }
        if (request.Description is not null) { agent.Description = request.Description; changes.Add("描述"); }
        if (request.SystemPrompt is not null) { agent.SystemPrompt = request.SystemPrompt; changes.Add("SystemPrompt"); }
        if (request.ModelId is not null) { agent.ModelId = request.ModelId; changes.Add("模型ID"); }
        if (request.ModelEndpointId.HasValue) { agent.ModelEndpointId = request.ModelEndpointId; changes.Add("模型端点"); }
        if (request.Status is not null && Enum.TryParse<AgentStatus>(request.Status, out var s))
        {
            if (agent.Status != s)
            {
                if (!IsValidTransition(agent.Status, s))
                    throw new InvalidOperationException($"不允许从 {agent.Status} 切换到 {s}");
                agent.Status = s;
                changes.Add($"状态({s})");
            }
        }
        if (request.Temperature.HasValue) { agent.Temperature = request.Temperature; changes.Add("Temperature"); }
        if (request.MaxTokens.HasValue) { agent.MaxTokens = request.MaxTokens; changes.Add("MaxTokens"); }
        if (request.TopP.HasValue) { agent.TopP = request.TopP; changes.Add("TopP"); }
        agent.UpdatedAt = DateTime.UtcNow;

        var result = await _repository.UpdateAsync(agent, ct);

        // 有变更时创建新版本
        if (changes.Count > 0)
        {
            var changedBy = request.Status ?? "system";
            await CreateVersionSnapshotAsync(agent, $"更新: {string.Join(", ", changes)}", changedBy, ct);
        }

        return Map(result);
    }

    /// <summary>状态切换（快捷方式）</summary>
    public async Task<AgentDto?> ChangeStatusAsync(Guid id, string newStatus, CancellationToken ct = default)
    {
        if (!Enum.TryParse<AgentStatus>(newStatus, out var targetStatus))
            throw new ArgumentException($"无效的状态值: {newStatus}");

        var agent = await _repository.GetByIdAsync(id, ct);
        if (agent is null) return null;

        if (!IsValidTransition(agent.Status, targetStatus))
            throw new InvalidOperationException($"不允许从 {agent.Status} 切换到 {targetStatus}");

        agent.Status = targetStatus;
        agent.UpdatedAt = DateTime.UtcNow;

        var result = await _repository.UpdateAsync(agent, ct);
        await CreateVersionSnapshotAsync(agent, $"状态切换: {agent.Status} -> {targetStatus}", "system", ct);

        return Map(result);
    }

    /// <summary>克隆 Agent（深拷贝配置）</summary>
    public async Task<AgentDto> CloneAsync(Guid sourceId, CloneAgentRequest request, CancellationToken ct = default)
    {
        var source = await _repository.GetByIdAsync(sourceId, ct);
        if (source is null)
            throw new InvalidOperationException("源 Agent 不存在");

        var clone = new Agent
        {
            Id = Guid.NewGuid(),
            Name = request.Name ?? $"{source.Name} (副本)",
            Description = source.Description,
            SystemPrompt = source.SystemPrompt,
            ModelId = source.ModelId,
            ModelEndpointId = source.ModelEndpointId,
            Temperature = source.Temperature,
            MaxTokens = source.MaxTokens,
            TopP = source.TopP,
            Status = AgentStatus.Draft,
            Version = "1.0.0",
            CreatedBy = request.CreatedBy ?? source.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 复制 Configurations
        clone.Configurations = source.Configurations.Select(c => new AgentConfiguration
        {
            Id = Guid.NewGuid(),
            AgentId = clone.Id,
            Key = c.Key,
            Value = c.Value,
            ValueType = c.ValueType
        }).ToList();

        var result = await _repository.AddAsync(clone, ct);
        await CreateVersionSnapshotAsync(clone, "从 " + source.Name + " 克隆", clone.CreatedBy, ct);

        return Map(result);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return true;
    }

    /// <summary>获取版本历史</summary>
    public async Task<List<AgentVersionDto>> GetVersionsAsync(Guid agentId, CancellationToken ct = default)
    {
        var versions = await _versionRepo.GetByAgentIdAsync(agentId, ct);
        return versions.Select(v => new AgentVersionDto(
            v.VersionNumber, v.VersionNumber, v.ChangeSummary, v.ChangedBy, v.CreatedAt)).ToList();
    }

    // ── 状态机验证 ──

    private static readonly Dictionary<AgentStatus, HashSet<AgentStatus>> AllowedTransitions = new()
    {
        [AgentStatus.Draft] = new() { AgentStatus.Active, AgentStatus.Archived },
        [AgentStatus.Active] = new() { AgentStatus.Inactive, AgentStatus.Running, AgentStatus.Archived },
        [AgentStatus.Inactive] = new() { AgentStatus.Active, AgentStatus.Archived },
        [AgentStatus.Running] = new() { AgentStatus.Paused, AgentStatus.Stopped },
        [AgentStatus.Paused] = new() { AgentStatus.Running, AgentStatus.Stopped },
        [AgentStatus.Stopped] = new() { AgentStatus.Draft, AgentStatus.Archived },
        [AgentStatus.Archived] = new() { }
    };

    private static bool IsValidTransition(AgentStatus from, AgentStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    // ── 版本快照 ──

    private async Task CreateVersionSnapshotAsync(Agent agent, string summary, string changedBy, CancellationToken ct)
    {
        var versionNumber = await _versionRepo.GetNextVersionNumberAsync(agent.Id, ct);
        var snapshot = JsonSerializer.Serialize(new
        {
            agent.Name, agent.Description, agent.SystemPrompt,
            agent.ModelId, agent.ModelEndpointId,
            agent.Temperature, agent.MaxTokens, agent.TopP,
            Status = agent.Status.ToString(),
            agent.Configurations,
            agent.McpEndpoints,
            agent.Skills
        });

        await _versionRepo.AddAsync(new AgentVersion
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            VersionNumber = versionNumber,
            SnapshotJson = snapshot,
            ChangeSummary = summary,
            ChangedBy = changedBy,
            CreatedAt = DateTime.UtcNow
        }, ct);

        // 更新 Agent 版本号
        agent.Version = $"{versionNumber}.0.0";
    }

    private static AgentDto Map(Agent a) => new(
        a.Id, a.Name, a.Description, a.SystemPrompt,
        a.ModelId, a.ModelEndpointId, a.ModelEndpoint?.ModelName,
        a.Status.ToString(), a.Version,
        a.CreatedBy, a.CreatedAt, a.UpdatedAt,
        a.Temperature, a.MaxTokens, a.TopP,
        a.Configurations.Select(c => new AgentConfigurationDto(c.Id, c.Key, c.Value, c.ValueType)).ToList(),
        a.Skills.Select(s => new AgentSkillDto(s.Id, s.SkillId, s.Priority, s.IsEnabled)).ToList());
}
