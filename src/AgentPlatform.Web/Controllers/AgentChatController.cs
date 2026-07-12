using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.AgentEngine.Harness;
using AgentPlatform.AgentEngine.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace AgentPlatform.Web.Controllers;

/// <summary>
/// Agent 对话 API（V2.0 CompleteAgent）：使用 CompleteAgentFactory 创建 CompleteAgent，
/// 集成中间件管道、技能系统、MCP 工具和上下文压缩。
/// 支持非流式 (send) 和流式 SSE (stream) 两种模式。
/// </summary>
[ApiController]
[Route("api/v1/agents/{agentId:guid}/chat")]
public class AgentChatController : ControllerBase
{
    private readonly IAgentRepository _agentRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IShortTermMemoryStore _memoryStore;
    private readonly CompleteAgentFactory _agentFactory;
    private readonly ILogger<AgentChatController> _logger;

    public AgentChatController(
        IAgentRepository agentRepo,
        ISessionRepository sessionRepo,
        IShortTermMemoryStore memoryStore,
        CompleteAgentFactory agentFactory,
        ILogger<AgentChatController> logger)
    {
        _agentRepo = agentRepo;
        _sessionRepo = sessionRepo;
        _memoryStore = memoryStore;
        _agentFactory = agentFactory;
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

        // 使用 CompleteAgentFactory 创建 CompleteAgent（含中间件、技能、MCP、压缩）
        var completeAgent = await _agentFactory.CreateAgentAsync(agent, ct: ct);
        await completeAgent.StartAsync(ct);

        try
        {
            var result = await completeAgent.RunAsync(request.Message, session.Id, ct);

            // 保存助手回复到短期记忆
            await _memoryStore.AddMessageAsync(session.Id, "assistant", result.Content, ct);
            var memoryTokens = await _memoryStore.GetTokenCountAsync(session.Id, ct);

            return Ok(new ApiResponse<ChatResult>(true, "OK", new ChatResult(
                session.Id, result.Content, 0, memoryTokens)));
        }
        finally
        {
            await completeAgent.StopAsync(ct);
            await completeAgent.DisposeAsync();
        }
    }

    /// <summary>
    /// SSE 流式对话端点（V2.0 CompleteAgent）
    /// 响应格式：data: {"type":"thinking"|"token"|"tool_call"|"done"|"error", ...}\n\n
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

            await WriteSseAsync(new { type = "session", sessionId = session.Id.ToString() }, ct);

            // 保存用户消息
            await _memoryStore.AddMessageAsync(session.Id, "user", request.Message, ct);

            // 创建并启动 CompleteAgent
            var completeAgent = await _agentFactory.CreateAgentAsync(agent, ct: ct);
            await completeAgent.StartAsync(ct);

            try
            {
                // 使用 CompleteAgent 流式执行对话
                var fullContent = "";
                await foreach (var delta in completeAgent.RunStreamingAsync(request.Message, session.Id, ct))
                {
                    switch (delta.Type)
                    {
                        case StreamDeltaType.Thinking:
                            if (!string.IsNullOrEmpty(delta.Thinking))
                                await WriteSseAsync(new { type = "thinking", content = delta.Thinking }, ct);
                            break;

                        case StreamDeltaType.Token:
                            if (!string.IsNullOrEmpty(delta.Content))
                            {
                                fullContent += delta.Content;
                                await WriteSseAsync(new { type = "token", content = delta.Content }, ct);
                            }
                            break;

                        case StreamDeltaType.ToolCall:
                            await WriteSseAsync(new
                            {
                                type = "tool_call",
                                name = delta.ToolCallName,
                                arguments = delta.ToolCallArgs,
                                result = delta.ToolCallResult
                            }, ct);
                            break;

                        case StreamDeltaType.Done:
                            // 保存助手回复
                            await _memoryStore.AddMessageAsync(session.Id, "assistant", delta.Content ?? fullContent, ct);
                            var memoryTokens = await _memoryStore.GetTokenCountAsync(session.Id, ct);

                            await WriteSseAsync(new
                            {
                                type = "done",
                                content = delta.Content ?? fullContent,
                                thinking = delta.Thinking,
                                toolCallCount = delta.ToolCallCount,
                                memoryTokens,
                                modelName = delta.ModelName,
                                inputTokens = delta.InputTokens,
                                outputTokens = delta.OutputTokens,
                                rawResponse = delta.RawResponse,
                                toolCalls = delta.ToolCalls.Select(tc => new
                                {
                                    name = tc.Name,
                                    arguments = tc.Arguments,
                                    result = tc.Result
                                }).ToList()
                            }, ct);
                            break;
                    }
                }
            }
            finally
            {
                await completeAgent.StopAsync(ct);
                await completeAgent.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE streaming error");
            await WriteSseAsync(new { type = "error", message = ex.Message }, ct);
        }
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
}

public record SendMessageRequest(string Message, string? UserId = "anonymous", Guid? SessionId = null);
public record ChatResult(Guid SessionId, string Reply, int ToolCallCount, int MemoryTokens = 0);
