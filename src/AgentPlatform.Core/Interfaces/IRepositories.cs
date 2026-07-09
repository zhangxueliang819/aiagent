using AgentPlatform.Core.Entities;

namespace AgentPlatform.Core.Interfaces;

public interface IAgentRepository
{
    Task<List<Agent>> GetAllAsync(CancellationToken ct = default);
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Agent> AddAsync(Agent agent, CancellationToken ct = default);
    Task<Agent> UpdateAsync(Agent agent, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IModelProviderRepository
{
    Task<List<ModelProvider>> GetAllAsync(CancellationToken ct = default);
    Task<ModelProvider?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ModelProvider> AddAsync(ModelProvider provider, CancellationToken ct = default);
    Task<ModelProvider> UpdateAsync(ModelProvider provider, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ModelEndpoint?> GetEndpointWithProviderAsync(Guid endpointId, CancellationToken ct = default);
    Task<ModelEndpoint> AddEndpointAsync(ModelEndpoint endpoint, CancellationToken ct = default);
    Task DeleteEndpointAsync(ModelEndpoint endpoint, CancellationToken ct = default);
}

public interface ISkillRepository
{
    Task<List<Skill>> GetAllAsync(CancellationToken ct = default);
    Task<Skill?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Skill> AddAsync(Skill skill, CancellationToken ct = default);
    Task<Skill> UpdateAsync(Skill skill, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ISessionRepository
{
    Task<List<Session>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Session> AddAsync(Session session, CancellationToken ct = default);
    Task<Conversation> AddConversationAsync(Conversation conversation, CancellationToken ct = default);
}

public interface IUsageRepository
{
    Task<List<UsageRecord>> GetByUserIdAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<UsageRecord> AddAsync(UsageRecord record, CancellationToken ct = default);
}

public interface IMcpEndpointRepository
{
    Task<List<McpEndpoint>> GetAllAsync(CancellationToken ct = default);
    Task<McpEndpoint?> GetByIdWithToolsAsync(Guid id, CancellationToken ct = default);
    Task<McpEndpoint> AddAsync(McpEndpoint endpoint, CancellationToken ct = default);
    Task<McpEndpoint> UpdateAsync(McpEndpoint endpoint, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IMcpToolRepository
{
    Task<List<McpTool>> GetByEndpointIdAsync(Guid endpointId, CancellationToken ct = default);
    Task AddRangeAsync(List<McpTool> tools, CancellationToken ct = default);
    Task ClearByEndpointIdAsync(Guid endpointId, CancellationToken ct = default);
}

public interface IAgentMcpEndpointRepository
{
    Task<List<AgentMcpEndpoint>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default);
    Task<AgentMcpEndpoint> AddAsync(AgentMcpEndpoint binding, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAgentSkillRepository
{
    Task<List<AgentSkill>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default);
    Task<AgentSkill> AddAsync(AgentSkill binding, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
