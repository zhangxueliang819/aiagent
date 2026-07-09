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

    /// <summary>LLM 温度参数 (0.0-2.0)，null 则使用模型默认值</summary>
    public double? Temperature { get; set; }
    /// <summary>最大输出 Token 数，null 则使用模型默认值</summary>
    public int? MaxTokens { get; set; }
    /// <summary>Top-P 采样参数 (0.0-1.0)，null 则使用模型默认值</summary>
    public double? TopP { get; set; }

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
    Archived,
    Running,
    Paused,
    Stopped
}
