namespace AgentPlatform.Core.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    /// <summary>关联的模型端点 ID（FK → ModelEndpoint）</summary>
    public Guid? ModelEndpointId { get; set; }
    /// <summary>保留旧的字符串 ModelId 用于兼容</summary>
    public string ModelId { get; set; } = string.Empty;
    public AgentStatus Status { get; set; } = AgentStatus.Draft;
    public string Version { get; set; } = "1.0.0";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AgentConfiguration> Configurations { get; set; } = new();
    public List<AgentSkill> Skills { get; set; } = new();
    public List<AgentMcpEndpoint> McpEndpoints { get; set; } = new();

    /// <summary>导航属性：关联的模型端点</summary>
    public ModelEndpoint? ModelEndpoint { get; set; }
}

public enum AgentStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}
