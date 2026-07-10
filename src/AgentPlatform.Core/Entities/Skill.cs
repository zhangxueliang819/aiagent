namespace AgentPlatform.Core.Entities;

public class Skill
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillType Type { get; set; } = SkillType.FunctionTool;
    public string Implementation { get; set; } = string.Empty;
    public string InputSchema { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>技能存储方式</summary>
    public SkillStorageType StorageType { get; set; } = SkillStorageType.Inline;

    /// <summary>StorageType=File/Directory 时的磁盘路径</summary>
    public string? StoragePath { get; set; }

    /// <summary>上传的原始文件名（StorageType=File 时有值）</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>技能包内文件清单 JSON</summary>
    public string? FileManifest { get; set; }

    public List<AgentSkill> AgentSkills { get; set; } = new();
}

/// <summary>
/// 技能类型：决定运行时映射目标
/// </summary>
public enum SkillType
{
    /// <summary>函数工具：注册为 MAF AIFunction，供 LLM function calling 调用</summary>
    FunctionTool = 0,
    /// <summary>Agent 知识技能：映射为 MAF AgentInlineSkill/AgentFileSkillsSource，渐进式披露指令+资源</summary>
    AgentSkill = 1,
    /// <summary>MCP 工具：来自 MCP Server 的工具</summary>
    McpTool = 2,

    // 保留旧值以兼容现有数据
    Tool = 0,
    Api = 0,
    Script = 1,
    Composite = 1
}

/// <summary>
/// 技能存储方式
/// </summary>
public enum SkillStorageType
{
    /// <summary>内容存储在数据库 Implementation 字段中</summary>
    Inline,
    /// <summary>用户通过前端上传的 .zip 包，解压后存于磁盘</summary>
    File,
    /// <summary>服务器本地目录挂载（管理员/CI 部署）</summary>
    Directory
}
