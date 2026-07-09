using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentPlatform.Infrastructure.Repositories;

public class AgentRepository : IAgentRepository
{
    private readonly AgentPlatformDbContext _db;

    public AgentRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<Agent>> GetAllAsync(CancellationToken ct = default)
        => await _db.Agents.Include(a => a.Configurations).Include(a => a.Skills)
            .Include(a => a.ModelEndpoint).ThenInclude(me => me!.ModelProvider)
            .ToListAsync(ct);

    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Agents.Include(a => a.Configurations).Include(a => a.Skills)
            .Include(a => a.ModelEndpoint).ThenInclude(me => me!.ModelProvider)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Agent> AddAsync(Agent agent, CancellationToken ct = default)
    {
        _db.Agents.Add(agent);
        await _db.SaveChangesAsync(ct);
        return agent;
    }

    public async Task<Agent> UpdateAsync(Agent agent, CancellationToken ct = default)
    {
        _db.Agents.Update(agent);
        await _db.SaveChangesAsync(ct);
        return agent;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var agent = await _db.Agents.FindAsync([id], ct);
        if (agent is not null)
        {
            _db.Agents.Remove(agent);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class ModelProviderRepository : IModelProviderRepository
{
    private readonly AgentPlatformDbContext _db;

    public ModelProviderRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<ModelProvider>> GetAllAsync(CancellationToken ct = default)
        => await _db.ModelProviders.Include(p => p.Endpoints).ToListAsync(ct);

    public async Task<ModelProvider?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ModelProviders.Include(p => p.Endpoints).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<ModelProvider> AddAsync(ModelProvider provider, CancellationToken ct = default)
    {
        _db.ModelProviders.Add(provider);
        await _db.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<ModelProvider> UpdateAsync(ModelProvider provider, CancellationToken ct = default)
    {
        _db.ModelProviders.Update(provider);
        await _db.SaveChangesAsync(ct);
        return provider;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.ModelProviders.FindAsync([id], ct);
        if (p is not null)
        {
            _db.ModelProviders.Remove(p);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<ModelEndpoint?> GetEndpointWithProviderAsync(Guid endpointId, CancellationToken ct = default)
        => await _db.ModelEndpoints.Include(e => e.ModelProvider)
            .FirstOrDefaultAsync(e => e.Id == endpointId, ct);

    public async Task<ModelEndpoint> AddEndpointAsync(ModelEndpoint endpoint, CancellationToken ct = default)
    {
        _db.ModelEndpoints.Add(endpoint);
        await _db.SaveChangesAsync(ct);
        return endpoint;
    }

    public async Task DeleteEndpointAsync(ModelEndpoint endpoint, CancellationToken ct = default)
    {
        _db.ModelEndpoints.Remove(endpoint);
        await _db.SaveChangesAsync(ct);
    }
}

public class SkillRepository : ISkillRepository
{
    private readonly AgentPlatformDbContext _db;

    public SkillRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<Skill>> GetAllAsync(CancellationToken ct = default)
        => await _db.Skills.ToListAsync(ct);

    public async Task<Skill?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Skills.FindAsync([id], ct).AsTask();

    public async Task<Skill> AddAsync(Skill skill, CancellationToken ct = default)
    {
        _db.Skills.Add(skill);
        await _db.SaveChangesAsync(ct);
        return skill;
    }

    public async Task<Skill> UpdateAsync(Skill skill, CancellationToken ct = default)
    {
        _db.Skills.Update(skill);
        await _db.SaveChangesAsync(ct);
        return skill;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Skills.FindAsync([id], ct);
        if (s is not null)
        {
            _db.Skills.Remove(s);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class SessionRepository : ISessionRepository
{
    private readonly AgentPlatformDbContext _db;

    public SessionRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<Session>> GetByUserIdAsync(string userId, CancellationToken ct = default)
        => await _db.Sessions.Include(s => s.Conversations).Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Sessions.Include(s => s.Conversations).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Session> AddAsync(Session session, CancellationToken ct = default)
    {
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<Conversation> AddConversationAsync(Conversation conversation, CancellationToken ct = default)
    {
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        return conversation;
    }
}

public class UsageRepository : IUsageRepository
{
    private readonly AgentPlatformDbContext _db;

    public UsageRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<UsageRecord>> GetByUserIdAsync(string userId, DateTime from, DateTime to, CancellationToken ct = default)
        => await _db.UsageRecords.Where(r => r.UserId == userId && r.CreatedAt >= from && r.CreatedAt <= to).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public async Task<UsageRecord> AddAsync(UsageRecord record, CancellationToken ct = default)
    {
        _db.UsageRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record;
    }
}

public class McpEndpointRepository : IMcpEndpointRepository
{
    private readonly AgentPlatformDbContext _db;

    public McpEndpointRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<McpEndpoint>> GetAllAsync(CancellationToken ct = default)
        => await _db.McpEndpoints.Include(e => e.Tools).ToListAsync(ct);

    public async Task<McpEndpoint?> GetByIdWithToolsAsync(Guid id, CancellationToken ct = default)
        => await _db.McpEndpoints.Include(e => e.Tools).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<McpEndpoint> AddAsync(McpEndpoint endpoint, CancellationToken ct = default)
    {
        _db.McpEndpoints.Add(endpoint);
        await _db.SaveChangesAsync(ct);
        return endpoint;
    }

    public async Task<McpEndpoint> UpdateAsync(McpEndpoint endpoint, CancellationToken ct = default)
    {
        _db.McpEndpoints.Update(endpoint);
        await _db.SaveChangesAsync(ct);
        return endpoint;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.McpEndpoints.FindAsync([id], ct);
        if (e is not null)
        {
            _db.McpEndpoints.Remove(e);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class McpToolRepository : IMcpToolRepository
{
    private readonly AgentPlatformDbContext _db;

    public McpToolRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<McpTool>> GetByEndpointIdAsync(Guid endpointId, CancellationToken ct = default)
        => await _db.McpTools.Where(t => t.McpEndpointId == endpointId).ToListAsync(ct);

    public async Task AddRangeAsync(List<McpTool> tools, CancellationToken ct = default)
    {
        _db.McpTools.AddRange(tools);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearByEndpointIdAsync(Guid endpointId, CancellationToken ct = default)
    {
        var tools = await _db.McpTools.Where(t => t.McpEndpointId == endpointId).ToListAsync(ct);
        _db.McpTools.RemoveRange(tools);
        await _db.SaveChangesAsync(ct);
    }
}

public class AgentMcpEndpointRepository : IAgentMcpEndpointRepository
{
    private readonly AgentPlatformDbContext _db;

    public AgentMcpEndpointRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<AgentMcpEndpoint>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default)
        => await _db.AgentMcpEndpoints.Include(x => x.McpEndpoint).Where(x => x.AgentId == agentId).ToListAsync(ct);

    public async Task<AgentMcpEndpoint> AddAsync(AgentMcpEndpoint binding, CancellationToken ct = default)
    {
        _db.AgentMcpEndpoints.Add(binding);
        await _db.SaveChangesAsync(ct);
        return binding;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.AgentMcpEndpoints.FindAsync([id], ct);
        if (b is not null)
        {
            _db.AgentMcpEndpoints.Remove(b);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class AgentSkillRepository : IAgentSkillRepository
{
    private readonly AgentPlatformDbContext _db;

    public AgentSkillRepository(AgentPlatformDbContext db) => _db = db;

    public async Task<List<AgentSkill>> GetByAgentIdAsync(Guid agentId, CancellationToken ct = default)
        => await _db.AgentSkills.Include(x => x.Skill).Where(x => x.AgentId == agentId).ToListAsync(ct);

    public async Task<AgentSkill> AddAsync(AgentSkill binding, CancellationToken ct = default)
    {
        _db.AgentSkills.Add(binding);
        await _db.SaveChangesAsync(ct);
        return binding;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.AgentSkills.FindAsync([id], ct);
        if (b is not null)
        {
            _db.AgentSkills.Remove(b);
            await _db.SaveChangesAsync(ct);
        }
    }
}
