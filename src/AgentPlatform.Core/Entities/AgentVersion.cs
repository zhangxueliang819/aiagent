namespace AgentPlatform.Core.Entities;

/// <summary>
/// Agent 版本快照：记录配置变更历史
/// 每次更新 Agent 时自动创建新版本
/// </summary>
public class AgentVersion
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public int VersionNumber { get; set; }

    /// <summary>版本变更时 Agent 的快照数据（JSON）</summary>
    public string SnapshotJson { get; set; } = string.Empty;

    /// <summary>变更摘要</summary>
    public string ChangeSummary { get; set; } = string.Empty;

    /// <summary>变更人</summary>
    public string ChangedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
