using AgentPlatform.Core.Entities;

namespace AgentPlatform.AgentEngine;

/// <summary>
/// Agent as Tool 适配器：将一个 Agent 包装为另一个 Agent 可调用的工具。
/// 对应 MAF 的 Agent as Tool 组合模式。
///
/// 当前阶段：提供抽象接口和基础实现。
/// MAF 阶段：直接使用 MAF 内置的 AgentAsTool 机制。
/// </summary>
public interface IAgentAsTool
{
    /// <summary>工具名称（作为 function name 暴露给 LLM）</summary>
    string ToolName { get; }

    /// <summary>工具描述（LLM 据此判断何时调用）</summary>
    string ToolDescription { get; }

    /// <summary>参数 Schema（JSON Schema 格式）</summary>
    string InputSchema { get; }

    /// <summary>执行子 Agent 任务</summary>
    Task<string> ExecuteAsync(string arguments, CancellationToken ct);
}

/// <summary>
/// Agent as Tool 基础实现：将一个 Agent 实体包装为可调用工具
/// </summary>
public class AgentAsToolWrapper : IAgentAsTool
{
    private readonly Agent _agent;
    private readonly Runtime.AgentRuntimeFactory _factory;

    public string ToolName { get; }
    public string ToolDescription { get; }
    public string InputSchema { get; }

    public AgentAsToolWrapper(Agent agent, Runtime.AgentRuntimeFactory factory)
    {
        _agent = agent;
        _factory = factory;

        // 构造工具元数据
        ToolName = $"agent_{NormalizeName(agent.Name)}";
        ToolDescription = agent.Description;
        InputSchema = """{"type":"object","properties":{"message":{"type":"string","description":"发送给子Agent的消息"}},"required":["message"]}""";
    }

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var response = await _factory.RunAsync(_agent, Guid.NewGuid(), arguments, ct);
        return response.Content;
    }

    private static string NormalizeName(string name)
        => name.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
}

/// <summary>
/// A2A 协议配置：Agent-to-Agent 通信的端点配置
/// 对应 MAF 的 A2AAgent。
///
/// 当前阶段：定义配置实体。
/// MAF 阶段：直接使用 MAF 的 A2AAgent。
/// </summary>
public class A2AConfiguration
{
    /// <summary>A2A 端点 URL</summary>
    public string? EndpointUrl { get; set; }

    /// <summary>认证 Token</summary>
    public string? AuthToken { get; set; }

    /// <summary>是否启用 A2A</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Agent Card 描述（用于服务发现）</summary>
    public string? AgentCardDescription { get; set; }

    /// <summary>支持的技能列表（对外暴露）</summary>
    public List<string> ExposedSkills { get; set; } = new();
}
