namespace AgentPlatform.Core.Entities;

/// <summary>
/// Agent 与 MCP 端点的多对多关联，控制哪些 MCP 工具对指定 Agent 可见
/// </summary>
public class AgentMcpEndpoint
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid McpEndpointId { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;

    public Agent? Agent { get; set; }
    public McpEndpoint? McpEndpoint { get; set; }
}
