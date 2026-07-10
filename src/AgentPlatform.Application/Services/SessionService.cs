using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class SessionService
{
    private readonly ISessionRepository _repository;

    public SessionService(ISessionRepository repository) => _repository = repository;

    public async Task<List<SessionDto>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var sessions = await _repository.GetByUserIdAsync(userId, ct);
        return sessions.Select(Map).ToList();
    }

    public async Task<SessionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _repository.GetByIdAsync(id, ct);
        return s is null ? null : Map(s);
    }

    public async Task<SessionDto> CreateAsync(string userId, Guid agentId, string title, CancellationToken ct = default)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AgentId = agentId,
            Title = title,
            Status = SessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        return Map(await _repository.AddAsync(session, ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return true;
    }

    private static SessionDto Map(Session s) => new(
        s.Id, s.Title, s.AgentId, s.Status.ToString(),
        s.CreatedAt,
        s.Conversations.Select(c => new ConversationDto(c.Id, c.Role, c.Content, c.TokenCount, c.CreatedAt)).ToList());
}
