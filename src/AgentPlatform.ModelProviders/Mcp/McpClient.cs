using System.Text;
using System.Text.Json;
using AgentPlatform.Core.Entities;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.ModelProviders.Mcp;

/// <summary>
/// MCP Client：通过 JSON-RPC 2.0 协议连接外部 MCP 端点
/// 支持 tools/list 和 tools/call 方法
/// </summary>
public class McpClient
{
    private readonly ILogger<McpClient> _logger;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public McpClient(ILogger<McpClient> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// 发现 MCP 端点提供的工具列表（JSON-RPC 2.0 tools/list）
    /// </summary>
    public async Task<List<McpTool>> DiscoverToolsAsync(McpEndpoint endpoint, CancellationToken ct)
    {
        _logger.LogInformation("Discovering tools from MCP endpoint: {Name} ({Url})",
            endpoint.Name, endpoint.EndpointUrl);

        if (string.IsNullOrWhiteSpace(endpoint.EndpointUrl))
        {
            _logger.LogWarning("MCP endpoint {Name} has no URL configured, returning empty tools", endpoint.Name);
            return [];
        }

        try
        {
            var request = new JsonRpcRequest { Method = "tools/list", Id = 1 };
            var responseJson = await SendJsonRpcAsync(endpoint, request, ct);
            var tools = ParseToolListResponse(responseJson, endpoint.Id);
            _logger.LogInformation("Discovered {Count} tools from {Name}", tools.Count, endpoint.Name);
            return tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover tools from MCP endpoint {Name}, falling back to cached", endpoint.Name);
            return [];
        }
    }

    /// <summary>
    /// 调用 MCP 端点上的指定工具（JSON-RPC 2.0 tools/call）
    /// </summary>
    public async Task<McpToolResult> InvokeToolAsync(McpTool tool, Dictionary<string, object?> arguments, CancellationToken ct)
    {
        _logger.LogInformation("Invoking MCP tool: {ToolName}", tool.ToolName);

        try
        {
            var request = new JsonRpcRequest
            {
                Method = "tools/call",
                Id = 2,
                Params = new { name = tool.ToolName, arguments }
            };

            var responseJson = await SendJsonRpcAsync(tool.McpEndpoint!, request, ct);
            return ParseToolCallResponse(responseJson, tool.ToolName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool invocation failed: {ToolName}", tool.ToolName);
            return new McpToolResult { Success = false, ToolName = tool.ToolName, Content = ex.Message };
        }
    }

    private async Task<string> SendJsonRpcAsync(McpEndpoint endpoint, JsonRpcRequest request, CancellationToken ct)
    {
        // 解析 AuthConfig
        var authConfig = string.IsNullOrWhiteSpace(endpoint.AuthConfig)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(endpoint.AuthConfig) ?? [];

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint.EndpointUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        // 处理认证
        if (authConfig.TryGetValue("type", out var authType))
        {
            switch (authType.ToLower())
            {
                case "bearer" when authConfig.TryGetValue("token", out var bearerToken):
                    httpRequest.Headers.Authorization = new("Bearer", bearerToken);
                    break;
                case "api_key" when authConfig.TryGetValue("key", out var apiKey):
                    httpRequest.Headers.Add("X-API-Key", apiKey);
                    break;
            }
        }

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static List<McpTool> ParseToolListResponse(string json, Guid endpointId)
    {
        var tools = new List<McpTool>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // JSON-RPC 2.0: {"jsonrpc":"2.0","result":{"tools":[...]},"id":1}
        if (root.TryGetProperty("result", out var result) && result.TryGetProperty("tools", out var toolArray))
        {
            foreach (var toolElem in toolArray.EnumerateArray())
            {
                tools.Add(new McpTool
                {
                    Id = Guid.NewGuid(),
                    McpEndpointId = endpointId,
                    ToolName = toolElem.GetProperty("name").GetString() ?? "unknown",
                    Description = toolElem.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                    InputSchema = toolElem.TryGetProperty("inputSchema", out var schema) ? schema.GetRawText() : "{}",
                    IsEnabled = true,
                    CachedAt = DateTime.UtcNow
                });
            }
        }
        return tools;
    }

    private static McpToolResult ParseToolCallResponse(string json, string toolName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var msg = error.TryGetProperty("message", out var m) ? m.GetString() ?? "Unknown error" : "Unknown error";
            return new McpToolResult { Success = false, ToolName = toolName, Content = msg };
        }

        var content = "";
        if (root.TryGetProperty("result", out var result))
        {
            if (result.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0)
            {
                // MCP 标准: content 是数组，每项有 type 和 text
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                        content += text.GetString();
                }
            }
            else
            {
                content = result.GetRawText();
            }
        }

        return new McpToolResult { Success = true, ToolName = toolName, Content = content };
    }
}

internal class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public string Method { get; set; } = string.Empty;
    public object? Params { get; set; }
    public int Id { get; set; }
}

public class McpToolResult
{
    public bool Success { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
