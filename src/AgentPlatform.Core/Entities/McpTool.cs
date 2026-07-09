namespace AgentPlatform.Core.Entities;

/// <summary>
/// MCP 端点下挂的工具定义，由 MCP Client 连接端点后动态发现并缓存
/// </summary>
public class McpTool
{
    public Guid Id { get; set; }
    public Guid McpEndpointId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchema { get; set; } = "{}"; // JSON Schema
    public bool IsEnabled { get; set; } = true;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;

    public McpEndpoint? McpEndpoint { get; set; }
}
