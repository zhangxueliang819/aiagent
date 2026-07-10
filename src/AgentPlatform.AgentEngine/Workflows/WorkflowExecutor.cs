namespace AgentPlatform.AgentEngine.Workflows;

/// <summary>
/// 工作流执行器接口：用于多 Agent 编排。
/// 对应 MAF 的 Workflow Builder 图编排引擎。
///
/// 当前阶段：定义抽象接口，支持顺序/并行/条件执行。
/// MAF 阶段：直接使用 MAF Workflow Builder + Executors + Edges + Checkpoint。
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>工作流名称</summary>
    string Name { get; }

    /// <summary>执行工作流</summary>
    Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct);
}

/// <summary>
/// 工作流上下文：包含输入、中间结果和状态
/// </summary>
public class WorkflowContext
{
    /// <summary>用户原始输入</summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>用户 ID</summary>
    public string? UserId { get; set; }

    /// <summary>Session ID</summary>
    public Guid? SessionId { get; set; }

    /// <summary>工作流中间结果（键：Agent Name，值：该 Agent 输出）</summary>
    public Dictionary<string, string> IntermediateResults { get; set; } = new();

    /// <summary>工作流状态快照（可用于 Checkpoint 恢复）</summary>
    public Dictionary<string, object?> State { get; set; } = new();

    /// <summary>取消令牌</summary>
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// 工作流执行结果
/// </summary>
public class WorkflowResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; set; } = true;

    /// <summary>最终输出</summary>
    public string? Output { get; set; }

    /// <summary>错误信息</summary>
    public string? Error { get; set; }

    /// <summary>执行步骤摘要</summary>
    public List<WorkflowStepInfo> Steps { get; set; } = new();
}

/// <summary>
/// 工作流执行步骤信息
/// </summary>
public class WorkflowStepInfo
{
    public string AgentName { get; set; } = string.Empty;
    public string? Output { get; set; }
    public TimeSpan Elapsed { get; set; }
    public bool Success { get; set; }
}

// ============================================================
// 内置工作流模式
// ============================================================

/// <summary>
/// 顺序工作流：按顺序执行多个 Agent，前一个输出作为后一个输入
/// </summary>
public class SequentialWorkflow<TAgent1, TAgent2> : IWorkflowExecutor
    where TAgent1 : IWorkflowExecutor
    where TAgent2 : IWorkflowExecutor
{
    private readonly TAgent1 _agent1;
    private readonly TAgent2 _agent2;

    public string Name => $"Sequential({_agent1.Name} → {_agent2.Name})";

    public SequentialWorkflow(TAgent1 agent1, TAgent2 agent2)
    {
        _agent1 = agent1;
        _agent2 = agent2;
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct)
    {
        var result = new WorkflowResult();
        var startTime = DateTime.UtcNow;

        // Step 1
        var r1 = await _agent1.ExecuteAsync(context, ct);
        result.Steps.Add(new WorkflowStepInfo
        {
            AgentName = _agent1.Name, Output = r1.Output,
            Elapsed = DateTime.UtcNow - startTime, Success = r1.Success
        });
        if (!r1.Success) { result.Success = false; result.Error = r1.Error; return result; }

        // Step 2
        context.IntermediateResults[_agent1.Name] = r1.Output ?? "";
        context.UserInput = r1.Output ?? context.UserInput;
        var r2 = await _agent2.ExecuteAsync(context, ct);
        result.Steps.Add(new WorkflowStepInfo
        {
            AgentName = _agent2.Name, Output = r2.Output,
            Elapsed = DateTime.UtcNow - startTime, Success = r2.Success
        });

        result.Output = r2.Output;
        result.Success = r2.Success;
        result.Error = r2.Error;
        return result;
    }
}

/// <summary>
/// 条件路由工作流：根据条件选择执行分支
/// </summary>
public class ConditionalWorkflow : IWorkflowExecutor
{
    private readonly IWorkflowExecutor _conditionAgent;
    private readonly IWorkflowExecutor _trueBranch;
    private readonly IWorkflowExecutor _falseBranch;

    public string Name => $"Condition({_conditionAgent.Name})";

    public ConditionalWorkflow(IWorkflowExecutor conditionAgent, IWorkflowExecutor trueBranch, IWorkflowExecutor falseBranch)
    {
        _conditionAgent = conditionAgent;
        _trueBranch = trueBranch;
        _falseBranch = falseBranch;
    }

    public async Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct)
    {
        var conditionResult = await _conditionAgent.ExecuteAsync(context, ct);
        var branch = conditionResult.Output?.Contains("true", StringComparison.OrdinalIgnoreCase) == true
            ? _trueBranch : _falseBranch;
        return await branch.ExecuteAsync(context, ct);
    }
}

/// <summary>
/// Human-in-the-Loop 审批节点：暂停工作流等待人工审批
/// </summary>
public class HumanApprovalNode : IWorkflowExecutor
{
    public string Name => "HumanApproval";

    /// <summary>审批提示信息</summary>
    public string ApprovalMessage { get; set; } = string.Empty;

    /// <summary>审批决策（外部设置）</summary>
    public bool? Approved { get; set; }

    public HumanApprovalNode(string message) => ApprovalMessage = message;

    public Task<WorkflowResult> ExecuteAsync(WorkflowContext context, CancellationToken ct)
    {
        if (Approved == true)
            return Task.FromResult(new WorkflowResult { Output = "approved" });

        if (Approved == false)
            return Task.FromResult(new WorkflowResult { Success = false, Error = "Rejected by human" });

        // 等待中——实际场景中应该挂起等待外部信号
        return Task.FromResult(new WorkflowResult
        {
            Success = false,
            Error = $"Approval pending: {ApprovalMessage}"
        });
    }
}
