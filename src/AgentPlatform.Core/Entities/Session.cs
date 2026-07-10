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
    /// <summary>会话过期时间，超过此时间未更新自动回收</summary>
    public DateTime? ExpiresAt { get; set; }
    /// <summary>
    /// AgentSession.StateBag 的序列化快照（JSON），用于双态设计：
    /// Session 实体（持久化）+ AgentSession.StateBag（运行时）
    /// </summary>
    public string? SerializedState { get; set; }

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
    public string Role { get; set; } = "user"; // user, assistant, system, tool
    public string Content { get; set; } = string.Empty;
    /// <summary>OpenAI Function Calling: tool call ID</summary>
    public string? ToolCallId { get; set; }
    /// <summary>OpenAI Function Calling: function name</summary>
    public string? ToolName { get; set; }
    /// <summary>OpenAI Function Calling: tool execution result (role=tool 时存储)</summary>
    public string? ToolResult { get; set; }
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Session? Session { get; set; }
}
