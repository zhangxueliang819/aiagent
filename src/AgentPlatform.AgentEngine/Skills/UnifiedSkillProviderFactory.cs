using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// 统一的技能提供器工厂：混合三种来源的技能
///   1. 数据库内联（StorageType=Inline）→ 注入 ChatClientAgent Instructions
///   2. 文件上传（StorageType=File/Directory）→ 通过 MAF AgentSkillsProvider 提供
///   3. 全局目录挂载技能
/// </summary>
public class UnifiedSkillProviderFactory
{
    private readonly ISkillRepository _skillRepo;
    private readonly IAgentSkillRepository _agentSkillRepo;
    private readonly DatabaseSkillSource _dbSkillSource;
    private readonly FunctionToolRegistry _functionToolRegistry;
    private readonly IConfiguration _config;
    private readonly ILogger<UnifiedSkillProviderFactory> _logger;

    public UnifiedSkillProviderFactory(
        ISkillRepository skillRepo,
        IAgentSkillRepository agentSkillRepo,
        DatabaseSkillSource dbSkillSource,
        FunctionToolRegistry functionToolRegistry,
        IConfiguration config,
        ILogger<UnifiedSkillProviderFactory> logger)
    {
        _skillRepo = skillRepo;
        _agentSkillRepo = agentSkillRepo;
        _dbSkillSource = dbSkillSource;
        _functionToolRegistry = functionToolRegistry;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 为指定 Agent 获取完整的技能配置
    /// </summary>
    public async Task<AgentSkillConfiguration> GetSkillConfigurationAsync(Guid agentId, CancellationToken ct = default)
    {
        var config = new AgentSkillConfiguration();

        // 1. FunctionTool 类型 → LLM function calling 工具
        config.FunctionTools = await _functionToolRegistry.GetFunctionToolsForAgentAsync(agentId, ct);

        // 2. AgentSkill (Inline) → 内联指令技能
        config.InlineAgentSkills = await _dbSkillSource.GetInlineAgentSkillsAsync(agentId, ct);

        // 3. AgentSkill (File/Directory) → 用于 AgentSkillsProvider 的技能目录
        config.FileSkillDirectories = await _dbSkillSource.GetFileSkillPathsAsync(agentId, ct);

        // 4. 文件/目录技能的元数据（用于流式路径的宣告）
        config.FileAgentSkills = await _dbSkillSource.GetFileAgentSkillsAsync(agentId, ct);

        // 5. 全局目录挂载技能
        var globalSkillsDir = _config["Skills:GlobalDirectory"] ?? "skills";
        if (Directory.Exists(globalSkillsDir))
        {
            config.GlobalSkillDirectories.Add(Path.GetFullPath(globalSkillsDir));
        }

        // 允许多个全局目录（逗号分隔）
        var extraDirs = _config["Skills:GlobalDirectories"];
        if (!string.IsNullOrEmpty(extraDirs))
        {
            foreach (var dir in extraDirs.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = dir.Trim();
                if (Directory.Exists(trimmed))
                    config.GlobalSkillDirectories.Add(Path.GetFullPath(trimmed));
            }
        }

        // 合并所有 AgentSkillsProvider 扫描目录
        config.AllProviderDirectories.AddRange(config.FileSkillDirectories);
        config.AllProviderDirectories.AddRange(config.GlobalSkillDirectories);

        _logger.LogInformation(
            "Skill configuration for agent {AgentId}: {FunctionTools} tools, {InlineSkills} inline skills, {FileSkillDirs} file skill dirs, {GlobalDirs} global dirs",
            agentId,
            config.FunctionTools.Count,
            config.InlineAgentSkills.Count,
            config.FileSkillDirectories.Count,
            config.GlobalSkillDirectories.Count);

        return config;
    }

    /// <summary>
    /// 为指定 Agent 创建 MAF AgentSkillsProvider（用于非流式 ChatClientAgent 路径）
    /// </summary>
    public Microsoft.Agents.AI.AgentSkillsProvider CreateAgentSkillsProvider(Guid agentId, ILoggerFactory loggerFactory)
    {
        // 从缓存或实时查询获取技能目录
        var config = _dbSkillSource.GetFileSkillPathsAsync(agentId).GetAwaiter().GetResult();
        var allDirs = new List<string>(config);

        // 全局目录
        var globalSkillsDir = _config["Skills:GlobalDirectory"] ?? "skills";
        if (Directory.Exists(globalSkillsDir))
            allDirs.Add(Path.GetFullPath(globalSkillsDir));

        var extraDirs = _config["Skills:GlobalDirectories"];
        if (!string.IsNullOrEmpty(extraDirs))
        {
            foreach (var dir in extraDirs.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = dir.Trim();
                if (Directory.Exists(trimmed))
                    allDirs.Add(Path.GetFullPath(trimmed));
            }
        }

        if (allDirs.Count == 0)
        {
            _logger.LogDebug("No file skill directories for agent {AgentId}, skipping AgentSkillsProvider", agentId);
            return null!;
        }

        _logger.LogInformation("Creating AgentSkillsProvider for agent {AgentId} with {DirCount} directories", agentId, allDirs.Count);

        return new Microsoft.Agents.AI.AgentSkillsProvider(
            allDirs,
            scriptRunner: null,
            fileOptions: null,
            options: null,
            loggerFactory: loggerFactory);
    }

}

/// <summary>
/// Agent 的完整技能配置
/// </summary>
public class AgentSkillConfiguration
{
    /// <summary>FunctionTool 技能列表（LLM function calling 目标）</summary>
    public List<Skill> FunctionTools { get; set; } = new();

    /// <summary>内联 AgentSkill（数据库中的 Markdown 指令）</summary>
    public List<Skill> InlineAgentSkills { get; set; } = new();

    /// <summary>文件/目录类型的技能存储路径（用于 AgentSkillsProvider）</summary>
    public List<string> FileSkillDirectories { get; set; } = new();

    /// <summary>文件/目录类型的技能元数据（用于流式路径的宣告）</summary>
    public List<Skill> FileAgentSkills { get; set; } = new();

    /// <summary>全局挂载的技能目录</summary>
    public List<string> GlobalSkillDirectories { get; set; } = new();

    /// <summary>所有 AgentSkillsProvider 需要扫描的目录（File + 全局）</summary>
    public List<string> AllProviderDirectories { get; set; } = new();
}
