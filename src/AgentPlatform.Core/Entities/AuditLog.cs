namespace AgentPlatform.Core.Entities;

/// <summary>
/// 审计日志：记录所有重要操作
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;   // Create, Update, Delete, Login, etc.
    public string EntityType { get; set; } = string.Empty; // Agent, ModelProvider, Skill, etc.
    public string? EntityId { get; set; }
    public string? Changes { get; set; } // JSON diff
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


