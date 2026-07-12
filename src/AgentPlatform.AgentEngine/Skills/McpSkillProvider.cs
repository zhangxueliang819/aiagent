using System.Reflection;
using System.Text.Json;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.Core.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// MCP 技能提供器 — 将 MCP 工具注册为 MAF AIFunction，注入 ChatClientAgent 的 Tools 列表。
///
/// 设计依据（Microsoft Agent Framework 文档 — 工具集成 和 MCP 章节）：
/// - 工具允许代理调用自定义函数，例如提取天气数据、查询数据库或调用 API
/// - MCP 工具作为 IChatClient 客户端中间件集成
/// - 通过将 MCP 工具包装为 AIFunction，利用 MAF 内置的 function calling 机制
///
/// MCP 工具的数据模型：
/// - McpEndpoint：MCP 服务器端点（Name, Url, 认证信息）
/// - McpTool：工具定义（Name, Description, InputSchema）
/// - AgentMcpEndpoint：Agent ↔ MCP 端点绑定关系
/// </summary>
public class McpSkillProvider
{
    private readonly ILogger<McpSkillProvider> _logger;
    private readonly McpToolClientFactory _clientFactory;

    // 运行时缓存的 MCP 工具定义（AgentId → List<AIFunction>）
    private readonly Dictionary<Guid, List<AIFunction>> _cachedTools = new();

    public McpSkillProvider(
        McpToolClientFactory clientFactory,
        ILogger<McpSkillProvider> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger;
    }

    /// <summary>
    /// 获取 Agent 绑定的 MCP 工具列表（转换为 AITool）
    /// </summary>
    public async Task<List<AITool>> GetAIToolsForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        var tools = await GetMcpToolsAsync(agentId, ct);
        return tools.Select(t => (AITool)t).ToList();
    }

    /// <summary>
    /// 获取 Agent 绑定的 MCP 工具列表（转换为 AIFunction）
    /// </summary>
    public async Task<List<AIFunction>> GetAIFunctionsForAgentAsync(Guid agentId, CancellationToken ct = default)
    {
        return await GetMcpToolsAsync(agentId, ct);
    }

    /// <summary>
    /// 获取 Agent 绑定的 MCP 工具定义列表
    /// </summary>
    private async Task<List<AIFunction>> GetMcpToolsAsync(Guid agentId, CancellationToken ct)
    {
        // 尝试从缓存读取
        if (_cachedTools.TryGetValue(agentId, out var cached))
        {
            _logger.LogDebug("Returning {Count} cached MCP tools for agent {AgentId}",
                cached.Count, agentId);
            return cached;
        }

        // 从 MCP 客户端工厂获取工具列表
        var mcpTools = await _clientFactory.GetAvailableToolsAsync(agentId, ct);

        var functions = new List<AIFunction>();
        foreach (var toolDef in mcpTools)
        {
            JsonElement jsonSchema;
            try
            {
                jsonSchema = JsonSerializer.Deserialize<JsonElement>(toolDef.InputSchema);
            }
            catch
            {
                jsonSchema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}");
            }

            var function = new McpAIFunction(
                name: toolDef.Name,
                description: toolDef.Description,
                jsonSchema: jsonSchema,
                endpointUrl: toolDef.EndpointUrl,
                executeAsync: (args, ct2) => _clientFactory.ExecuteToolAsync(
                    toolDef.EndpointUrl, toolDef.Name, args, ct2));

            functions.Add(function);
        }

        // 缓存结果
        _cachedTools[agentId] = functions;

        _logger.LogInformation(
            "Loaded {Count} MCP tools for agent {AgentId}",
            functions.Count, agentId);

        return functions;
    }

    /// <summary>刷新 Agent 的 MCP 工具缓存</summary>
    public void InvalidateCache(Guid agentId)
    {
        _cachedTools.Remove(agentId);
        _logger.LogDebug("Invalidated MCP tool cache for agent {AgentId}", agentId);
    }

    /// <summary>清空所有 MCP 工具缓存</summary>
    public void ClearCache()
    {
        _cachedTools.Clear();
        _logger.LogDebug("Cleared all MCP tool caches");
    }
}

// ══════════════════════════════════════════════════════════════════
//  辅助类型
// ══════════════════════════════════════════════════════════════════

/// <summary>MCP 工具定义（从 MCP Server 发现）</summary>
public class McpToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchema { get; set; } = "{}";
    public string EndpointUrl { get; set; } = string.Empty;
}

/// <summary>
/// MCP 工具客户端工厂 — 管理 MCP 连接和工具发现
/// </summary>
public class McpToolClientFactory
{
    private readonly ILogger<McpToolClientFactory> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // 模拟的 MCP 工具注册表（生产环境应由 MCP Server 动态发现）
    private readonly Dictionary<string, List<McpToolDefinition>> _registeredEndpoints = new();

    public McpToolClientFactory(
        IHttpClientFactory httpClientFactory,
        ILogger<McpToolClientFactory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>注册一个 MCP 端点及其工具列表</summary>
    public void RegisterEndpoint(string endpointUrl, List<McpToolDefinition> tools)
    {
        _registeredEndpoints[endpointUrl] = tools;
        _logger.LogInformation("Registered MCP endpoint {Url} with {Count} tools",
            endpointUrl, tools.Count);
    }

    /// <summary>
    /// 获取指定 Agent 可用的 MCP 工具列表
    /// 生产环境应通过 Agent 绑定的 McpEndpoint 从数据库中查询
    /// </summary>
    public virtual Task<List<McpToolDefinition>> GetAvailableToolsAsync(
        Guid agentId, CancellationToken ct)
    {
        var allTools = _registeredEndpoints.Values
            .SelectMany(v => v)
            .ToList();

        return Task.FromResult(allTools);
    }

    /// <summary>
    /// 执行 MCP 工具调用
    /// </summary>
    public virtual async Task<string> ExecuteToolAsync(
        string endpointUrl, string toolName, string argumentsJson, CancellationToken ct)
    {
        _logger.LogInformation("Executing MCP tool {ToolName} on {Endpoint}",
            toolName, endpointUrl);

        try
        {
            // 生产环境应通过 MCP 协议（SSE/STDIO）发送请求
            // 这里使用 HTTP 模拟 MCP 工具调用
            var httpClient = _httpClientFactory.CreateClient($"mcp-{endpointUrl.GetHashCode()}");

            var payload = new
            {
                tool = toolName,
                arguments = argumentsJson
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(endpointUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync(ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool {ToolName} execution failed", toolName);
            return JsonSerializer.Serialize(new
            {
                error = $"MCP tool '{toolName}' execution failed: {ex.Message}"
            });
        }
    }
}

/// <summary>
/// MCP AIFunction — 将 MCP 工具包装为 MAF AIFunction
/// </summary>
public class McpAIFunction : AIFunction
{
    private readonly string _name;
    private readonly string _description;
    private readonly JsonElement _jsonSchema;
    private readonly string _endpointUrl;
    private readonly Func<string, CancellationToken, Task<string>> _executeAsync;
    private static readonly MethodInfo ExecuteMethod = typeof(McpAIFunction).GetMethod(nameof(ExecuteAsync))!;

    /// <summary>MCP 端点 URL</summary>
    public string EndpointUrl => _endpointUrl;

    public McpAIFunction(
        string name,
        string description,
        JsonElement jsonSchema,
        string endpointUrl,
        Func<string, CancellationToken, Task<string>> executeAsync)
    {
        _name = name;
        _description = description;
        _jsonSchema = jsonSchema;
        _endpointUrl = endpointUrl;
        _executeAsync = executeAsync;
    }

    public override string Name => _name;
    public override string Description => _description;
    public override JsonElement JsonSchema => _jsonSchema;
    public override MethodInfo UnderlyingMethod => ExecuteMethod;

    /// <summary>将 AIFunctionArguments 序列化为 JSON 后调用 MCP 工具</summary>
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken = default)
    {
        string argsJson;
        if (arguments is { Count: > 0 })
        {
            var dict = new Dictionary<string, object?>();
            foreach (var key in arguments.Keys)
                dict[key] = arguments[key];
            argsJson = JsonSerializer.Serialize(dict);
        }
        else
        {
            argsJson = "{}";
        }

        var result = await _executeAsync(argsJson, cancellationToken);
        return result;
    }

    /// <summary>供反射使用的占位方法</summary>
    public Task<string> ExecuteAsync(string arguments, CancellationToken ct)
        => _executeAsync(arguments, ct);
}
