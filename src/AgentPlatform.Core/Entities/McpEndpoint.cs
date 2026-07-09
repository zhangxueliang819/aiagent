namespace AgentPlatform.Core.Entities;

public class McpEndpoint
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string Protocol { get; set; } = "sse"; // sse, stdio
    public string AuthConfig { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<McpTool> Tools { get; set; } = new();
    public List<AgentMcpEndpoint> AgentMcpEndpoints { get; set; } = new();
}
