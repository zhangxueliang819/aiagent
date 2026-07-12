using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using AgentPlatform.AgentEngine.Middleware;
using AgentPlatform.AgentEngine.Runtime;
using AgentPlatform.Core.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Harness;

/// <summary>
/// Complete Agent — 基于 MAF ChatClientAgent 的全功能 Agent 封装。
/// 整合技能集成、中间件管道和生命周期管理。
///
/// 设计理念（基于 Microsoft Agent Framework 文档）：
/// - ChatClientAgent：使用 MAF 内置 Agent Loop（IChatClient + 工具调用）
/// - 中间件层：中间件管道提供 Before/After/Error 拦截点
/// - 技能集成：FunctionTool（AIFunction）+ AgentSkill（AgentSkillsProvider）+ MCP 工具
/// - 生命周期：完整的状态机管理（Draft → Active → Running → Paused → Stopped → Archived）
/// - 上下文管理：历史消息管理委托给 MAF AIContextProvider
/// </summary>
public class CompleteAgent : IAsyncDisposable
{
    private readonly Microsoft.Agents.AI.ChatClientAgent _innerAgent;
    private readonly MiddlewarePipeline _pipeline;
    private readonly ILogger<CompleteAgent> _logger;
    private readonly Agent _entity;
    private CompleteAgentState _state = CompleteAgentState.Draft;
    private readonly object _stateLock = new();
    private bool _disposed;

    // ══════════════════════════════════════════════════════════════
    //  公共属性
    // ══════════════════════════════════════════════════════════════

    /// <summary>Agent 的唯一标识</summary>
    public Guid Id => _entity.Id;

    /// <summary>Agent 名称</summary>
    public string Name => _entity.Name;

    /// <summary>Agent 实体（数据模型）</summary>
    public Agent Entity => _entity;

    /// <summary>当前生命周期状态</summary>
    public CompleteAgentState State => _state;

    // ══════════════════════════════════════════════════════════════
    //  生命周期事件
    // ══════════════════════════════════════════════════════════════

    /// <summary>Agent 执行出错时触发</summary>
    public event Func<CompleteAgent, Exception, Task>? OnError;

    /// <summary>每次对话执行完成时触发</summary>
    public event Func<CompleteAgent, AgentRunResult, Task>? OnRunCompleted;

    // ══════════════════════════════════════════════════════════════
    //  构造函数（通过 CompleteAgentBuilder 创建）
    // ══════════════════════════════════════════════════════════════

    internal CompleteAgent(
        Agent entity,
        Microsoft.Agents.AI.ChatClientAgent innerAgent,
        MiddlewarePipeline pipeline,
        ILogger<CompleteAgent> logger)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _innerAgent = innerAgent ?? throw new ArgumentNullException(nameof(innerAgent));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════
    //  生命周期管理
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 启动 Agent：将状态从 Draft 切换为 Active
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        TransitionState(CompleteAgentState.Draft, CompleteAgentState.Active);
        _logger.LogInformation("CompleteAgent [{Name}] started (Active)", Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止 Agent：切换到 Stopped 状态
    /// </summary>
    public Task StopAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            _state = CompleteAgentState.Stopped;
        }
        _logger.LogInformation("CompleteAgent [{Name}] stopped", Name);
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════
    //  对话执行
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 执行一次对话（非流式），自动经过中间件管道。
    /// 注意：历史消息管理由 MAF AIContextProvider 负责，CompleteAgent 不直接操作历史。
    /// </summary>
    public async Task<AgentRunResult> RunAsync(
        string userMessage,
        Guid? sessionId = null,
        CancellationToken ct = default)
    {
        ThrowIfNotExecutable();

        var context = new AgentMiddlewareContext
        {
            AgentId = _entity.Id,
            AgentName = _entity.Name,
            SessionId = sessionId,
            UserMessage = userMessage,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // 1. 执行 Before 中间件管道
            var middlewareResult = await _pipeline.ExecuteBeforeAsync(context, ct);
            if (middlewareResult is not null)
            {
                // 中间件中断请求，返回短路响应
                return new AgentRunResult
                {
                    Content = middlewareResult.ToString() ?? "Request blocked by middleware",
                    Interrupted = true,
                    AgentName = Name,
                    SessionId = sessionId,
                    DurationMs = (DateTime.UtcNow - context.StartedAt).TotalMilliseconds
                };
            }

            // 2. 构建消息列表（仅 System + 用户消息，历史由 MAF AIContextProvider 管理）
            var messages = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(_entity.SystemPrompt))
                messages.Add(new ChatMessage(ChatRole.System, _entity.SystemPrompt));
            messages.Add(new ChatMessage(ChatRole.User, userMessage));

            // 3. 标记为 Running
            SetRunning();

            // 4. 执行 MAF Agent（AIContextProvider 负责历史/上下文管理）
            _logger.LogInformation("CompleteAgent [{Name}] executing run...", Name);
            var response = await _innerAgent.RunAsync(messages, session: null, options: null, ct);

            var content = response.Messages.LastOrDefault()?.Text ?? string.Empty;
            var modelName = string.IsNullOrEmpty(_entity.ModelId) ? _entity.Name : _entity.ModelId;
            var result = new AgentRunResult
            {
                Content = content,
                AgentName = Name,
                SessionId = sessionId,
                ModelName = modelName,
                InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
                OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
                DurationMs = (DateTime.UtcNow - context.StartedAt).TotalMilliseconds,
                Interrupted = false
            };

            // 5. 执行 After 中间件管道
            await _pipeline.ExecuteAfterAsync(context, result, ct);

            // 6. 触发完成事件
            if (OnRunCompleted is not null)
                await OnRunCompleted.Invoke(this, result);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "CompleteAgent [{Name}] run failed", Name);

            if (OnError is not null)
                await OnError.Invoke(this, ex);

            await _pipeline.ExecuteErrorAsync(context, ex, ct);

            return new AgentRunResult
            {
                Content = $"Error: {ex.Message}",
                AgentName = Name,
                SessionId = sessionId,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - context.StartedAt).TotalMilliseconds,
                Interrupted = true
            };
        }
        finally
        {
            // 恢复到 Active（非终态）
            lock (_stateLock)
            {
                if (_state == CompleteAgentState.Running)
                    _state = CompleteAgentState.Active;
            }
        }
    }

    /// <summary>
    /// 执行流式对话，自动经过中间件管道。
    /// 注意：历史消息管理由 MAF AIContextProvider 负责，CompleteAgent 不直接操作历史。
    /// </summary>
    public async IAsyncEnumerable<StreamingDelta> RunStreamingAsync(
        string userMessage,
        Guid? sessionId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 将 try-catch（不允许 yield）和 yield return（不允许 catch）分离到两个方法
        List<StreamingDelta> deltas;
        try
        {
            deltas = await CollectStreamingDeltasAsync(userMessage, sessionId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "CompleteAgent [{Name}] streaming failed", Name);
            deltas = new List<StreamingDelta>
            {
                new() { Type = StreamDeltaType.Done, Content = $"Error: {ex.Message}" }
            };
        }

        foreach (var delta in deltas)
            yield return delta;
    }

    /// <summary>
    /// 收集流式增量数据（可包含 try-catch，不含 yield return）
    /// 支持 Thinking（推理内容）、Token（文本增量）、ToolCall（工具调用）和 Done（完成事件）
    /// 注意：历史消息管理由 MAF AIContextProvider 负责，CompleteAgent 不直接操作历史。
    /// </summary>
    private async Task<List<StreamingDelta>> CollectStreamingDeltasAsync(
        string userMessage, Guid? sessionId, CancellationToken ct)
    {
        var deltas = new List<StreamingDelta>();

        var context = new AgentMiddlewareContext
        {
            AgentId = _entity.Id,
            AgentName = _entity.Name,
            SessionId = sessionId,
            UserMessage = userMessage,
            StartedAt = DateTime.UtcNow
        };

        // 1. Before 中间件
        var middlewareResult = await _pipeline.ExecuteBeforeAsync(context, ct);
        if (middlewareResult is not null)
        {
            deltas.Add(new StreamingDelta
            {
                Type = StreamDeltaType.Token,
                Content = middlewareResult.ToString() ?? "Request blocked by middleware"
            });
            return deltas;
        }

        // 2. 构建消息列表（仅 System + 用户消息，历史由 MAF AIContextProvider 管理）
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(_entity.SystemPrompt))
            messages.Add(new ChatMessage(ChatRole.System, _entity.SystemPrompt));
        messages.Add(new ChatMessage(ChatRole.User, userMessage));

        // 3. 标记为 Running
        SetRunning();

        // 确定模型名（一次赋值，流式 update 中无 ModelId）
        var modelName = string.IsNullOrEmpty(_entity.ModelId) ? _entity.Name : _entity.ModelId;

        // 4. 通过 MAF 获取流式响应（含 Agent Loop + Tool Calling）
        var responseStream = _innerAgent.RunStreamingAsync(messages, session: null, options: null, ct);

        string fullContent = "";
        string fullThinking = "";
        int inputTokens = 0, outputTokens = 0;
        var toolCalls = new List<ToolCallInfo>();
        var rawAdditionalProps = new Dictionary<string, object?>();
        string? rawResponseId = null;
        string? rawFinishReason = null;

        await foreach (var update in responseStream)
        {
            // 收集原始响应属性
            rawResponseId ??= update.ResponseId;
            rawFinishReason ??= update.FinishReason?.ToString();
            if (update.AdditionalProperties is { Count: > 0 })
            {
                foreach (var kv in update.AdditionalProperties)
                    rawAdditionalProps[kv.Key] = kv.Value;
            }

            // 提取推理/思考内容
            string? thinkingDelta = null;
            if (update.AdditionalProperties?.TryGetValue("reasoning_content", out var r) == true)
                thinkingDelta = r?.ToString();

            if (!string.IsNullOrEmpty(thinkingDelta))
            {
                fullThinking += thinkingDelta;
                deltas.Add(new StreamingDelta
                {
                    Type = StreamDeltaType.Thinking,
                    Thinking = thinkingDelta
                });
            }

            // 提取文本增量
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullContent += update.Text;
                deltas.Add(new StreamingDelta
                {
                    Type = StreamDeltaType.Token,
                    Content = update.Text
                });
            }

            // 提取工具调用（FunctionCallContent）
            foreach (var fc in update.Contents.OfType<FunctionCallContent>())
            {
                var args = fc.Arguments is IDictionary<string, object?> dict
                    ? System.Text.Json.JsonSerializer.Serialize(dict)
                    : fc.Arguments?.ToString() ?? "{}";

                toolCalls.Add(new ToolCallInfo { Name = fc.Name, Arguments = args });
                deltas.Add(new StreamingDelta
                {
                    Type = StreamDeltaType.ToolCall,
                    ToolCallName = fc.Name,
                    ToolCallArgs = args
                });
            }

            // 提取 Usage
            if (update.AdditionalProperties?.TryGetValue("usage", out var usageObj) == true
                && usageObj is System.Text.Json.JsonElement usageElem)
            {
                if (usageElem.TryGetProperty("prompt_tokens", out var pt))
                    inputTokens = pt.GetInt32();
                if (usageElem.TryGetProperty("completion_tokens", out var ct2))
                    outputTokens = ct2.GetInt32();
            }
        }

        // 5. After 中间件
        var result = new AgentRunResult
        {
            Content = fullContent,
            AgentName = Name,
            SessionId = sessionId,
            DurationMs = (DateTime.UtcNow - context.StartedAt).TotalMilliseconds,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
        await _pipeline.ExecuteAfterAsync(context, result, ct);

        // 6. 构建原始响应 JSON
        var rawResponseObj = new Dictionary<string, object?>
        {
            ["modelId"] = modelName,
            ["responseId"] = rawResponseId,
            ["finishReason"] = rawFinishReason,
            ["inputTokens"] = inputTokens,
            ["outputTokens"] = outputTokens,
            ["text"] = fullContent,
            ["thinking"] = string.IsNullOrEmpty(fullThinking) ? null : fullThinking
        };
        if (toolCalls.Count > 0)
        {
            rawResponseObj["toolCalls"] = toolCalls.Select(tc => new
            {
                name = tc.Name,
                arguments = tc.Arguments,
                result = tc.Result
            }).ToList();
        }
        if (rawAdditionalProps.Count > 0)
        {
            var filtered = rawAdditionalProps
                .Where(kv => kv.Key is not ("reasoning_content" or "usage"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            if (filtered.Count > 0)
                rawResponseObj["additionalProperties"] = filtered;
        }
        var rawResponseJson = System.Text.Json.JsonSerializer.Serialize(rawResponseObj, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        // 7. Done 事件（含聚合元信息）
        deltas.Add(new StreamingDelta
        {
            Type = StreamDeltaType.Done,
            Content = fullContent,
            Thinking = string.IsNullOrEmpty(fullThinking) ? null : fullThinking,
            ToolCallCount = toolCalls.Count,
            ToolCalls = toolCalls,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            RawResponse = rawResponseJson
        });

        if (OnRunCompleted is not null)
            await OnRunCompleted.Invoke(this, result);

        return deltas;
    }

    // ══════════════════════════════════════════════════════════════
    //  内部辅助方法
    // ══════════════════════════════════════════════════════════════

    private void ThrowIfNotExecutable()
    {
        if (_state is not (CompleteAgentState.Active or CompleteAgentState.Running))
            throw new InvalidOperationException(
                $"Agent '{Name}' is in state '{_state}' and cannot execute. " +
                $"Start the agent first via StartAsync().");
    }

    private void SetRunning()
    {
        lock (_stateLock)
        {
            if (_state == CompleteAgentState.Active)
                _state = CompleteAgentState.Running;
        }
    }

    private void TransitionState(CompleteAgentState from, CompleteAgentState to)
    {
        lock (_stateLock)
        {
            if (_state != from)
                throw new InvalidOperationException(
                    $"Cannot transition from '{_state}' to '{to}'. Expected current state: '{from}'.");
            _state = to;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  资源释放
    // ══════════════════════════════════════════════════════════════

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_state is CompleteAgentState.Running)
        {
            lock (_stateLock) { _state = CompleteAgentState.Stopped; }
        }

        // ChatClientAgent 未实现 IAsyncDisposable/IDisposable，但可能包含可释放资源
        if (((object)_innerAgent) is IDisposable disposable)
            disposable.Dispose();

        _logger.LogInformation("CompleteAgent [{Name}] disposed", Name);
    }
}

// ══════════════════════════════════════════════════════════════════
//  辅助类型
// ══════════════════════════════════════════════════════════════════

/// <summary>CompleteAgent 生命周期状态</summary>
public enum CompleteAgentState
{
    /// <summary>草稿（初始状态，不可执行）</summary>
    Draft,
    /// <summary>活跃（可执行对话）</summary>
    Active,
    /// <summary>运行中（当前正在处理请求）</summary>
    Running,
    /// <summary>已停止（不可执行）</summary>
    Stopped
}

/// <summary>Agent 运行结果</summary>
public class AgentRunResult
{
    /// <summary>响应文本</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Agent 名称</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>会话 ID</summary>
    public Guid? SessionId { get; set; }

    /// <summary>模型名称</summary>
    public string? ModelName { get; set; }

    /// <summary>输入 Token 数</summary>
    public int InputTokens { get; set; }

    /// <summary>输出 Token 数</summary>
    public int OutputTokens { get; set; }

    /// <summary>耗时（毫秒）</summary>
    public double DurationMs { get; set; }

    /// <summary>是否被中间件中断</summary>
    public bool Interrupted { get; set; }

    /// <summary>错误信息（如有）</summary>
    public string? Error { get; set; }

    /// <summary>是否成功</summary>
    public bool IsSuccess => string.IsNullOrEmpty(Error) && !Interrupted;
}
