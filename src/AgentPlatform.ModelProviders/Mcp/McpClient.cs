using System.Text;
using System.Text.Json;
using AgentPlatform.Core.Entities;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.ModelProviders.Mcp;

/// <summary>
/// MCP Client：连接外部 MCP 端点（SSE 协议），发现工具列表并调用工具
/// 当前为模拟实现，展示完整的接口契约
/// </summary>
public class McpClient
{
    private readonly ILogger<McpClient> _logger;
    private readonly HttpClient _httpClient;

    public McpClient(ILogger<McpClient> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// 连接 MCP 端点并发现其提供的工具列表
    /// </summary>
    public async Task<List<McpTool>> DiscoverToolsAsync(McpEndpoint endpoint, CancellationToken ct)
    {
        _logger.LogInformation("Discovering tools from MCP endpoint: {Name} ({Url})",
            endpoint.Name, endpoint.EndpointUrl);

        try
        {
            // 真实实现：向 MCP 端点发送 tools/list 请求
            // var request = new { jsonrpc = "2.0", method = "tools/list", id = 1 };
            // var response = await _httpClient.PostAsync(endpoint.EndpointUrl, new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"), ct);
            // var tools = ParseToolList(await response.Content.ReadAsStringAsync(ct));

            // 模拟：返回预置工具列表
            var simulatedTools = new List<McpTool>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    McpEndpointId = endpoint.Id,
                    ToolName = $"{endpoint.Name}_search",
                    Description = "Search documents in knowledge base",
                    InputSchema = """{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""",
                    CachedAt = DateTime.UtcNow
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    McpEndpointId = endpoint.Id,
                    ToolName = $"{endpoint.Name}_query",
                    Description = "Query database with SQL",
                    InputSchema = """{"type":"object","properties":{"sql":{"type":"string"}},"required":["sql"]}""",
                    CachedAt = DateTime.UtcNow
                }
            };

            _logger.LogInformation("Discovered {Count} tools from MCP endpoint {Name}",
                simulatedTools.Count, endpoint.Name);

            return simulatedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from MCP endpoint {Name}", endpoint.Name);
            return new List<McpTool>();
        }
    }

    /// <summary>
    /// 调用 MCP 端点上的指定工具
    /// </summary>
    public async Task<McpToolResult> InvokeToolAsync(McpTool tool, Dictionary<string, object?> arguments, CancellationToken ct)
    {
        _logger.LogInformation("Invoking MCP tool: {ToolName} on endpoint {EndpointId}",
            tool.ToolName, tool.McpEndpointId);

        try
        {
            // 真实实现：向 MCP 端点发送 tools/call 请求
            // var request = new
            // {
            //     jsonrpc = "2.0",
            //     method = "tools/call",
            //     @params = new { name = tool.ToolName, arguments },
            //     id = 2
            // };
            // var response = await _httpClient.PostAsync(endpointUrl, ...);

            // 模拟执行结果
            return new McpToolResult
            {
                Success = true,
                ToolName = tool.ToolName,
                Content = JsonSerializer.Serialize(new
                {
                    status = "simulated",
                    tool = tool.ToolName,
                    args_received = arguments.Count,
                    result = $"MCP tool '{tool.ToolName}' executed successfully (simulated)"
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool invocation failed: {ToolName}", tool.ToolName);
            return new McpToolResult { Success = false, ToolName = tool.ToolName, Content = ex.Message };
        }
    }
}

public class McpToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
