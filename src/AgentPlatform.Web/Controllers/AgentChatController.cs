using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentPlatform.Application.DTOs;
using AgentPlatform.Application.Services;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.AgentEngine.Runtime;
using AgentPlatform.ModelProviders.Simulated;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

/// <summary>
/// Agent 对话 API：完整的 Agent → LLM → FunctionCall → Skill/MCP 链路
/// 支持非流式 (send) 和流式 SSE (stream) 两种模式
/// </summary>
[ApiController]
[Route("api/v1/agents/{agentId:guid}/chat")]
public class AgentChatController : ControllerBase
{
    private readonly IAgentRepository _agentRepo;
    private readonly IAgentSkillRepository _agentSkillRepo;
    private readonly IAgentMcpEndpointRepository _agentMcpRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IShortTermMemoryStore _memoryStore;
    private readonly AgentRuntime _runtime;
    private readonly ModelRouter _modelRouter;
    private readonly SimulatedModelProvider _fallbackLlm;
    private readonly ILogger<AgentChatController> _logger;

    /// <summary>LLM 上下文窗口最大 Token 数（含 system prompt 和 function defs 的预留）</summary>
    private const int MaxContextTokens = 4096;

    public AgentChatController(
        IAgentRepository agentRepo,
        IAgentSkillRepository agentSkillRepo,
        IAgentMcpEndpointRepository agentMcpRepo,
        ISessionRepository sessionRepo,
        IShortTermMemoryStore memoryStore,
        AgentRuntime runtime,
        ModelRouter modelRouter,
        SimulatedModelProvider fallbackLlm,
        ILogger<AgentChatController> logger)
    {
        _agentRepo = agentRepo;
        _agentSkillRepo = agentSkillRepo;
        _agentMcpRepo = agentMcpRepo;
        _sessionRepo = sessionRepo;
        _memoryStore = memoryStore;
        _runtime = runtime;
        _modelRouter = modelRouter;
        _fallbackLlm = fallbackLlm;
        _logger = logger;
    }

    /// <summary>
    /// 向 Agent 发送消息（非流式），返回完整响应
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<ApiResponse<ChatResult>>> SendMessage(
        Guid agentId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        var agent = await _agentRepo.GetByIdAsync(agentId, ct);
        if (agent is null) return NotFound(new ApiResponse<ChatResult>(false, "Agent not found", null));

        // 加载 Agent 关联的 Skill 和 MCP
        var agentSkills = await _agentSkillRepo.GetByAgentIdAsync(agentId, ct);
        var agentMcps = await _agentMcpRepo.GetByAgentIdAsync(agentId, ct);

        var skills = agentSkills.Where(x => x.IsEnabled).Select(x => x.Skill!).Where(s => s.IsEnabled).ToList();
        var mcpEndpoints = agentMcps.Where(x => x.IsEnabled).Select(x => x.McpEndpoint!).Where(e => e.IsEnabled).ToList();

        _logger.LogInformation("Agent {AgentName}: {SkillCount} skills, {McpCount} MCP endpoints",
            agent.Name, skills.Count, mcpEndpoints.Count);

        // 获取或创建 Session
        Session session;
        if (request.SessionId.HasValue)
        {
            session = await _sessionRepo.GetByIdAsync(request.SessionId.Value, ct)
                ?? throw new InvalidOperationException("Session not found");
        }
        else
        {
            session = await _sessionRepo.AddAsync(new Session
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                UserId = request.UserId ?? "anonymous",
                Title = request.Message[..Math.Min(request.Message.Length, 50)],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, ct);
        }

        // 保存用户消息到短期记忆
        await _memoryStore.AddMessageAsync(session.Id, "user", request.Message, ct);

        // 获取上下文窗口（Token 预算管理）
        var contextWindow = await _memoryStore.GetContextWindowAsync(session.Id, MaxContextTokens, ct);
        var history = contextWindow.Select(m => new Conversation
        {
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();

        // 解析 LLM Provider（真实 API 或回退到模拟）
        var llm = await ResolveLlmAsync(agent, ct);

        // 设置模拟 LLM 的函数列表
        if (llm is SimulatedModelProvider simulated)
        {
            simulated.RegisterFunctions(CollectFunctionDefinitions(skills, mcpEndpoints));
        }

        // 执行 Agent Runtime
        var response = await _runtime.RunAsync(
            agent, llm,
            history,
            request.Message,
            skills,
            mcpEndpoints,
            ct);

        // 保存助手回复到短期记忆
        await _memoryStore.AddMessageAsync(session.Id, "assistant", response.Content, ct);

        // 获取上下文统计
        var memoryTokens = await _memoryStore.GetTokenCountAsync(session.Id, ct);

        return Ok(new ApiResponse<ChatResult>(true, "OK", new ChatResult(
            session.Id, response.Content, response.ToolCallCount, memoryTokens)));
    }

    /// <summary>
    /// SSE 流式对话端点
    /// 响应格式：data: {"type":"token"|"tool_call"|"tool_result"|"done"|"error", ...}\n\n
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamMessage(
        Guid agentId,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            var agent = await _agentRepo.GetByIdAsync(agentId, ct);
            if (agent is null)
            {
                await WriteSseAsync(new { type = "error", message = "Agent not found" }, ct);
                return;
            }

            // 加载关联
            var agentSkills = await _agentSkillRepo.GetByAgentIdAsync(agentId, ct);
            var agentMcps = await _agentMcpRepo.GetByAgentIdAsync(agentId, ct);
            var skills = agentSkills.Where(x => x.IsEnabled).Select(x => x.Skill!).Where(s => s.IsEnabled).ToList();
            var mcpEndpoints = agentMcps.Where(x => x.IsEnabled).Select(x => x.McpEndpoint!).Where(e => e.IsEnabled).ToList();

            // 获取或创建 Session
            Session session;
            if (request.SessionId.HasValue)
            {
                session = await _sessionRepo.GetByIdAsync(request.SessionId.Value, ct)
                    ?? throw new InvalidOperationException("Session not found");
            }
            else
            {
                session = await _sessionRepo.AddAsync(new Session
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    UserId = request.UserId ?? "anonymous",
                    Title = request.Message[..Math.Min(request.Message.Length, 50)],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, ct);
            }

            // 发送 session 信息
            await WriteSseAsync(new { type = "session", sessionId = session.Id.ToString() }, ct);

            // 保存用户消息
            await _memoryStore.AddMessageAsync(session.Id, "user", request.Message, ct);

            // 获取上下文窗口
            var contextWindow = await _memoryStore.GetContextWindowAsync(session.Id, MaxContextTokens, ct);
            var history = contextWindow.Select(m => new Conversation
            {
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }).ToList();

            var llm = await ResolveLlmAsync(agent, ct);

            // 注册函数
            if (llm is SimulatedModelProvider simulated)
            {
                simulated.RegisterFunctions(CollectFunctionDefinitions(skills, mcpEndpoints));
            }

            // 流式执行 Agent Runtime（累积 chunk 用于后续保存）
            var responseChunks = new List<string>();
            await foreach (var chunk in _runtime.RunStreamAsync(
                agent, llm, history, request.Message, skills, mcpEndpoints, ct))
            {
                responseChunks.Add(chunk);
                await WriteSseAsync(new { type = "token", content = chunk }, ct);
            }

            var fullResponse = string.Concat(responseChunks);

            // 保存助手回复
            await _memoryStore.AddMessageAsync(session.Id, "assistant", fullResponse, ct);

            // 发送完成事件
            await WriteSseAsync(new
            {
                type = "done",
                toolCallCount = 0,
                memoryTokens = await _memoryStore.GetTokenCountAsync(session.Id, ct)
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE streaming error");
            await WriteSseAsync(new { type = "error", message = ex.Message }, ct);
        }
    }

    /// <summary>
    /// 解析 Agent 对应的 LLM Provider：优先使用真实 API，未配置则回退到模拟 LLM
    /// </summary>
    private async Task<IModelProvider> ResolveLlmAsync(Agent agent, CancellationToken ct)
    {
        var realProvider = await _modelRouter.ResolveAsync(agent, ct);
        if (realProvider is not null)
        {
            _logger.LogInformation("Using real LLM provider for Agent {AgentName}", agent.Name);
            return realProvider;
        }

        _logger.LogInformation("Agent {AgentName} has no model endpoint configured, using simulated LLM", agent.Name);
        return _fallbackLlm;
    }

    /// <summary>
    /// 写入 SSE 格式数据帧
    /// </summary>
    private async Task WriteSseAsync(object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// 收集所有可用函数的定义（供 LLM function calling 使用）
    /// </summary>
    private static Dictionary<string, (string Description, string Schema)> CollectFunctionDefinitions(
        List<Skill> skills, List<McpEndpoint> mcpEndpoints)
    {
        var defs = new Dictionary<string, (string Description, string Schema)>();

        foreach (var skill in skills)
        {
            defs[skill.Name] = (skill.Description, skill.InputSchema);
        }

        foreach (var mcp in mcpEndpoints)
        {
            foreach (var tool in mcp.Tools)
            {
                defs[tool.ToolName] = (tool.Description, tool.InputSchema);
            }
        }

        return defs;
    }
}

public record SendMessageRequest(string Message, string? UserId = "anonymous", Guid? SessionId = null);
public record ChatResult(Guid SessionId, string Reply, int ToolCallCount, int MemoryTokens = 0);
