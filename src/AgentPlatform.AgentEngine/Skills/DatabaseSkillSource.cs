using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// 数据库技能源：从 Skills 表读取 AgentSkill 类型记录，为 Agent 提供知识技能。
///
/// MAF 1.13.0 架构说明：
/// MAF 不再提供 SK/AutoGen 时代的 AgentSkillsSource/AgentInlineSkill 基类。
/// 代理知识/技能的注入方式改为：
///   1. 内联 AgentSkill → 注入 ChatClientAgent 的 Instructions (SystemPrompt)
///   2. File/Directory AgentSkill → 作为文件上下文通过 AIContextProvider 注入
///   3. FunctionTool → 通过 AIFunctionFactory 注册为 AIFunction 工具
///
/// 本类负责从数据库加载 AgentSkill 类型记录，由 AgentContextProvider/AgentRuntimeFactory 注入。
/// </summary>
public class DatabaseSkillSource
{
    private readonly ISkillRepository _skillRepo;
    private readonly IAgentSkillRepository _agentSkillRepo;
    private readonly ILogger<DatabaseSkillSource> _logger;

    public DatabaseSkillSource(
        ISkillRepository skillRepo,
        IAgentSkillRepository agentSkillRepo,
        ILogger<DatabaseSkillSource> logger)
    {
        _skillRepo = skillRepo;
        _agentSkillRepo = agentSkillRepo;
        _logger = logger;
    }

    /// <summary>
    /// 获取 Agent 绑定的 AgentSkill 类型技能
    /// </summary>
    public async Task<List<Skill>> GetInlineAgentSkillsAsync(Guid agentId, CancellationToken ct = default)
    {
        var bindings = await _agentSkillRepo.GetByAgentIdAsync(agentId, ct);
        var skillIds = bindings
            .Where(b => b.IsEnabled)
            .OrderBy(b => b.Priority)
            .Select(b => b.SkillId)
            .ToList();

        var allSkills = new List<Skill>();
        foreach (var skillId in skillIds)
        {
            var skill = await _skillRepo.GetByIdAsync(skillId, ct);
            if (skill is not null
                && skill.Type == SkillType.AgentSkill
                && skill.StorageType == SkillStorageType.Inline
                && skill.IsEnabled)
            {
                allSkills.Add(skill);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} inline AgentSkills for agent {AgentId}",
            allSkills.Count, agentId);

        return allSkills;
    }

    /// <summary>
    /// 获取 Agent 绑定的 File/Directory 类型技能的存储路径列表
    /// </summary>
    public async Task<List<string>> GetFileSkillPathsAsync(Guid agentId, CancellationToken ct = default)
    {
        var bindings = await _agentSkillRepo.GetByAgentIdAsync(agentId, ct);
        var skillIds = bindings
            .Where(b => b.IsEnabled)
            .Select(b => b.SkillId)
            .ToList();

        var paths = new List<string>();
        foreach (var skillId in skillIds)
        {
            var skill = await _skillRepo.GetByIdAsync(skillId, ct);
            if (skill is not null
                && skill.Type == SkillType.AgentSkill
                && (skill.StorageType == SkillStorageType.File || skill.StorageType == SkillStorageType.Directory)
                && skill.IsEnabled
                && !string.IsNullOrEmpty(skill.StoragePath))
            {
                paths.Add(skill.StoragePath);
            }
        }

        _logger.LogInformation(
            "Found {Count} file/directory skill paths for agent {AgentId}",
            paths.Count, agentId);

        return paths;
    }
}
