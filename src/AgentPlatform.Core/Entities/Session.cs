namespace AgentPlatform.Core.Entities;

public class Session
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid AgentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Conversation> Conversations { get; set; } = new();
}

public enum SessionStatus
{
    Active,
    Completed,
    Archived
}

public class Conversation
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = "user"; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Session? Session { get; set; }
}
