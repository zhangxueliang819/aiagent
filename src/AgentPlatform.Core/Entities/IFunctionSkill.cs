namespace AgentPlatform.Core.Entities;

/// <summary>
/// 可注册的函数技能契约：FunctionTool 类型技能的运行时执行委托。
/// 实现类通过 DI 注册到 FunctionSkillRegistry，技能管理前端可发现和选择。
/// 符合 MAF AIFunction 模式 — 由 IChatClient 在 function calling 中自动调用。
/// </summary>
public interface IFunctionSkill
{
    /// <summary>执行器唯一名称，与 Skill.Implementation 匹配</summary>
    string Name { get; }

    /// <summary>执行器描述</summary>
    string Description { get; }

    /// <summary>JSON Schema，描述输入参数结构</summary>
    string InputSchemaJson { get; }

    /// <summary>执行函数逻辑，返回结果 JSON</summary>
    Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct = default);
}
