using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Skills;

/// <summary>
/// 统一的技能提供器工厂：混合三种来源的技能
///   1. 数据库内联（StorageType=Inline）→ 注入 ChatClientAgent Instructions
///   2. 文件上传（StorageType=File）→ 由 AIContextProvider 注入文件上下文
///   3. 目录挂载（StorageType=Directory）
///
/// MAF 1.13.0：不再使用 AgentSkillsProvider（SK 概念），改用 Instructions 注入 + FunctionTool。
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
        config.ToolDefinitions = FunctionToolRegistry.BuildToolDefinitions(config.FunctionTools);

        // 2. AgentSkill (Inline) → 内联指令技能
        config.InlineAgentSkills = await _dbSkillSource.GetInlineAgentSkillsAsync(agentId, ct);

        // 3. AgentSkill (File/Directory) → 基于文件的技能路径
        config.FileSkillPaths = await _dbSkillSource.GetFileSkillPathsAsync(agentId, ct);

        // 4. 全局目录挂载技能
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

        _logger.LogInformation(
            "Skill configuration for agent {AgentId}: {FunctionTools} tools, {InlineSkills} inline skills, {FilePaths} file paths, {GlobalDirs} global dirs",
            agentId,
            config.FunctionTools.Count,
            config.InlineAgentSkills.Count,
            config.FileSkillPaths.Count,
            config.GlobalSkillDirectories.Count);

        return config;
    }

    /// <summary>
    /// 获取所有可用于绑定的技能列表（管理后台用）
    /// </summary>
    public async Task<List<Skill>> GetAllAvailableSkillsAsync(CancellationToken ct = default)
    {
        return await _skillRepo.GetAllAsync(ct);
    }
}

/// <summary>
/// Agent 的完整技能配置
/// </summary>
public class AgentSkillConfiguration
{
    /// <summary>FunctionTool 技能列表（LLM function calling 目标）</summary>
    public List<Skill> FunctionTools { get; set; } = new();

    /// <summary>OpenAI 兼容格式的 Tool Definitions</summary>
    public List<object> ToolDefinitions { get; set; } = new();

    /// <summary>内联 AgentSkill（数据库中的 Markdown 指令）</summary>
    public List<Skill> InlineAgentSkills { get; set; } = new();

    /// <summary>文件/目录类型的技能存储路径</summary>
    public List<string> FileSkillPaths { get; set; } = new();

    /// <summary>全局挂载的技能目录</summary>
    public List<string> GlobalSkillDirectories { get; set; } = new();
}
