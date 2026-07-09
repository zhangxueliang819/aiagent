using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// 技能自动注册扩展方法
/// 用法：services.AddSkill<MySkillExecutor>("Tool", "我的技能描述")
/// </summary>
public static class SkillRegistrationExtensions
{
    /// <summary>
    /// 注册单个技能执行器
    /// </summary>
    public static IServiceCollection AddSkill<T>(this IServiceCollection services)
        where T : class, ISkillExecutor
    {
        services.TryAddSingleton<ISkillExecutor, T>();
        return services;
    }

    /// <summary>
    /// 从指定程序集自动扫描并注册所有 ISkillExecutor 实现
    /// </summary>
    public static IServiceCollection AddSkillsFromAssembly(this IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var executorTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISkillExecutor).IsAssignableFrom(t));

        foreach (var type in executorTypes)
        {
            services.TryAddSingleton(typeof(ISkillExecutor), type);
        }

        return services;
    }
}
