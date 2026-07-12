namespace AgentPlatform.Core.Entities;

/// <summary>
/// FunctionTool 执行器注册表（Singleton）。
/// 开发者在 Startup 时通过 Register() 注册 IFunctionSkill 实现，
/// 前端通过 API 发现可用执行器，用户在技能管理中按名称选择绑定。
/// 运行时由 FunctionToolRegistry 解析并创建 MAF AIFunction。
/// </summary>
public class FunctionSkillRegistry
{
    private readonly Dictionary<string, IFunctionSkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>注册一个函数技能执行器</summary>
    public void Register(IFunctionSkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills[skill.Name] = skill;
    }

    /// <summary>按名称获取执行器</summary>
    public IFunctionSkill? Get(string name)
    {
        return _skills.TryGetValue(name, out var skill) ? skill : null;
    }

    /// <summary>获取所有已注册的执行器</summary>
    public IReadOnlyList<IFunctionSkill> GetAll() => _skills.Values.ToList().AsReadOnly();
}
