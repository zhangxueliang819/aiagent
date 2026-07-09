using Microsoft.AspNetCore.SignalR;

namespace AgentPlatform.Web.Hubs;

/// <summary>
/// SignalR Hub：实时对话通信
/// 
/// 提供以下实时事件流：
/// - TokenStream: 流式输出 LLM 生成的 Token
/// - ToolCallExecuted: 工具调用执行通知（含参数和结果）
/// - AgentStatusChanged: Agent 状态变更通知
/// - MessageReceived: 完整消息接收通知
/// 
/// 客户端方法：
/// - JoinSession: 加入指定会话频道
/// - LeaveSession: 离开会话频道
/// - SendMessage: 发送用户消息（由 SignalR 触发对话流程）
/// </summary>
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 客户端加入指定会话的 SignalR 组
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client {ConnectionId} joined session {SessionId}", Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// 客户端离开会话组
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client {ConnectionId} left session {SessionId}", Context.ConnectionId, sessionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// SignalR 事件广播服务：封装向会话组推送实时事件
/// </summary>
public class ChatEventBroadcaster
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatEventBroadcaster> _logger;

    public ChatEventBroadcaster(IHubContext<ChatHub> hubContext, ILogger<ChatEventBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>广播 Token 流片段</summary>
    public async Task SendTokenAsync(string sessionId, string token, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("TokenStream", token, ct);
    }

    /// <summary>广播工具调用结果</summary>
    public async Task SendToolCallAsync(string sessionId, string toolName,
        Dictionary<string, object?> arguments, string result, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("ToolCallExecuted", new
        {
            name = toolName,
            arguments,
            result,
            timestamp = DateTime.UtcNow
        }, ct);
    }

    /// <summary>广播 Agent 状态变更</summary>
    public async Task SendStatusChangeAsync(string sessionId, string status, string? message = null, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("AgentStatusChanged", new
        {
            status,
            message,
            timestamp = DateTime.UtcNow
        }, ct);
    }

    /// <summary>广播完整消息</summary>
    public async Task SendMessageAsync(string sessionId, string role, string content, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("MessageReceived", new
        {
            role,
            content,
            timestamp = DateTime.UtcNow
        }, ct);
    }

    /// <summary>广播错误</summary>
    public async Task SendErrorAsync(string sessionId, string error, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync("ErrorOccurred", new
        {
            message = error,
            timestamp = DateTime.UtcNow
        }, ct);
    }
}
