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

    /// <summary>
    /// MAF Agent 类型标记：ChatClientAgent / HarnessAgent / Custom
    /// null 表示未启用 MAF 运行时
    /// </summary>
    public string? MafAgentType { get; set; }
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

/// <summary>
/// MAF Agent 运行时类型
/// </summary>
public enum MafAgentType
{
    /// <summary>标准 ChatClientAgent：基于 IChatClient 的对话型 Agent</summary>
    ChatClientAgent,
    /// <summary>电池全配 HarnessAgent：ChatClientAgent + Compaction/Todo/Mode/File 全套能力</summary>
    HarnessAgent,
    /// <summary>自定义 Agent：继承 AIAgent 基类</summary>
    Custom
}
