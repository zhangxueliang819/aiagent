using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.AgentEngine.Mcp;

/// <summary>
/// MCP 工具桥接器：将现有的 McpClient 工具封装为 MAF AITool。
///
/// MAF 1.13.0 架构说明：
/// MAF 不内置 MCP 客户端（LocalMcpTools），MCP 集成通过三方库或自定义实现。
/// 本类负责将 MCP 工具包装为 AITool（MEAI 工具抽象），注入 ChatOptions.Tools。
///
/// 如需原生 MCP 支持，可使用 mcpdotnet 等社区库创建 IChatClient 中间件。
/// </summary>
public class McpToolBridge
{
    private readonly ModelProviders.Mcp.McpClient _mcpClient;
    private readonly IMcpEndpointRepository _endpointRepo;
    private readonly IMcpToolRepository _toolRepo;
    private readonly ILogger<McpToolBridge> _logger;

    public McpToolBridge(
        ModelProviders.Mcp.McpClient mcpClient,
        IMcpEndpointRepository endpointRepo,
        IMcpToolRepository toolRepo,
        ILogger<McpToolBridge> logger)
    {
        _mcpClient = mcpClient;
        _endpointRepo = endpointRepo;
        _toolRepo = toolRepo;
        _logger = logger;
    }

    /// <summary>
    /// 获取 Agent 关联的所有 MCP 工具
    /// </summary>
    public async Task<List<McpTool>> GetToolsForAgentAsync(
        List<McpEndpoint> endpoints, CancellationToken ct = default)
    {
        var allTools = new List<McpTool>();

        foreach (var endpoint in endpoints.Where(e => e.IsEnabled))
        {
            try
            {
                var tools = await _mcpClient.DiscoverToolsAsync(endpoint, ct);
                allTools.AddRange(tools);
                _logger.LogInformation(
                    "Discovered {Count} tools from MCP endpoint {Name}",
                    tools.Count, endpoint.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to discover tools from MCP endpoint {Name}",
                    endpoint.Name);
            }
        }

        return allTools;
    }

    /// <summary>
    /// 获取 MAF AITool 列表（可直接注入 ChatOptions.Tools）
    /// </summary>
    public async Task<List<AITool>> GetAIToolsForAgentAsync(
        List<McpEndpoint> endpoints, CancellationToken ct = default)
    {
        var tools = new List<AITool>();
        var mcpTools = await GetToolsForAgentAsync(endpoints, ct);

        foreach (var tool in mcpTools)
        {
            var aiFunction = AIFunctionFactory.Create(
                method: (string arguments, CancellationToken ct2) =>
                    _mcpClient.InvokeToolAsync(tool,
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments) ?? new(),
                        ct2).ContinueWith(t => t.Result.Content),
                name: tool.ToolName,
                description: tool.Description);

            tools.Add(aiFunction);
        }

        return tools;
    }

    /// <summary>
    /// 构建 MCP 工具调用委托列表（兼容现有的 FunctionCallHandler）
    /// </summary>
    public List<(McpTool Tool, Func<McpTool, Dictionary<string, object?>, CancellationToken, Task<string>> Invoker)>
        BuildInvokers(List<McpTool> tools)
    {
        return tools.Select(t => (
            Tool: t,
            Invoker: new Func<McpTool, Dictionary<string, object?>, CancellationToken, Task<string>>(
                async (tool, args, ct2) =>
                {
                    var result = await _mcpClient.InvokeToolAsync(tool, args, ct2);
                    return result.Content;
                })
        )).ToList();
    }
}
