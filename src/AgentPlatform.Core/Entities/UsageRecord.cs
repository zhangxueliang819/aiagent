namespace AgentPlatform.Core.Entities;

public class UsageRecord
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid? AgentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal Cost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
