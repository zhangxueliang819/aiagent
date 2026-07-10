using System.Diagnostics;
using AgentPlatform.Core.Entities;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Telemetry;

/// <summary>
/// Agent 可观测性：基于 .NET ActivitySource 提供 OpenTelemetry 兼容的 Tracing + Metrics。
///
/// 当前阶段：使用 System.Diagnostics.ActivitySource（.NET 内置，OpenTelemetry 兼容）。
/// MAF 阶段：MAF 内置 OpenTelemetry 集成后，可无缝对接。
/// </summary>
public class AgentTelemetry
{
    private static readonly ActivitySource _source = new("AgentPlatform.AgentEngine", "1.0.0");
    private readonly ILogger<AgentTelemetry> _logger;

    public AgentTelemetry(ILogger<AgentTelemetry> logger) => _logger = logger;

    /// <summary>
    /// 开始一次 Agent 调用 Trace
    /// </summary>
    public Activity? StartAgentCall(Agent agent, string userMessage, Guid? sessionId)
    {
        var activity = _source.StartActivity("AgentCall", ActivityKind.Server);
        if (activity is null) return null;

        activity.SetTag("agent.id", agent.Id.ToString());
        activity.SetTag("agent.name", agent.Name);
        activity.SetTag("agent.type", agent.MafAgentType ?? "Legacy");
        activity.SetTag("agent.model", agent.ModelId);
        activity.SetTag("session.id", sessionId?.ToString());
        activity.SetTag("message.length", userMessage.Length);

        _logger.LogDebug("Telemetry: AgentCall started for {AgentName}", agent.Name);
        return activity;
    }

    /// <summary>
    /// 记录工具调用
    /// </summary>
    public void RecordToolCall(Activity? activity, string toolName, long elapsedMs, bool success)
    {
        if (activity is null) return;

        activity.AddEvent(new ActivityEvent("tool_call", tags: new ActivityTagsCollection
        {
            ["tool.name"] = toolName,
            ["tool.elapsed_ms"] = elapsedMs,
            ["tool.success"] = success
        }));
    }

    /// <summary>
    /// 结束 Agent 调用 Trace
    /// </summary>
    public void EndAgentCall(Activity? activity, bool success, int? inputTokens = null, int? outputTokens = null)
    {
        if (activity is null) return;

        activity.SetTag("success", success);
        if (inputTokens.HasValue) activity.SetTag("tokens.input", inputTokens.Value);
        if (outputTokens.HasValue) activity.SetTag("tokens.output", outputTokens.Value);
        activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.Stop();

        _logger.LogDebug("Telemetry: AgentCall ended, success={Success}", success);
    }

    /// <summary>
    /// 记录工作流执行
    /// </summary>
    public Activity? StartWorkflow(string workflowName)
    {
        var activity = _source.StartActivity("Workflow", ActivityKind.Internal);
        activity?.SetTag("workflow.name", workflowName);
        return activity;
    }

    /// <summary>
    /// 记录工作流步骤
    /// </summary>
    public void RecordWorkflowStep(Activity? activity, string agentName, long elapsedMs, bool success)
    {
        if (activity is null) return;

        activity.AddEvent(new ActivityEvent("workflow_step", tags: new ActivityTagsCollection
        {
            ["step.agent"] = agentName,
            ["step.elapsed_ms"] = elapsedMs,
            ["step.success"] = success
        }));
    }

    /// <summary>
    /// 结束工作流 Trace
    /// </summary>
    public void EndWorkflow(Activity? activity, bool success)
    {
        activity?.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity?.Stop();
    }
}
