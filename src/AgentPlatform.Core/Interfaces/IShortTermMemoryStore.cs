namespace AgentPlatform.Core.Interfaces;

/// <summary>
/// 短期记忆存储：管理会话内的对话上下文窗口
/// 支持 Token 计数、滑动窗口截断、多轮对话记忆
/// </summary>
public interface IShortTermMemoryStore
{
    /// <summary>
    /// 获取会话的最近 N 轮对话（自动按 Token 上限截断）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="maxTokens">最大 Token 上限，超出部分从最早的消息开始截断</param>
    /// <param name="ct"></param>
    /// <returns>适合注入 LLM 上下文的消息列表</returns>
    Task<List<MemoryEntry>> GetContextWindowAsync(Guid sessionId, int maxTokens, CancellationToken ct = default);

    /// <summary>
    /// 向会话中追加一条消息
    /// </summary>
    Task AddMessageAsync(Guid sessionId, string role, string content, CancellationToken ct = default);

    /// <summary>
    /// 获取会话的总 Token 用量
    /// </summary>
    Task<int> GetTokenCountAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 清理会话的短期记忆（归档时调用）
    /// </summary>
    Task ClearAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// 短期记忆条目
/// </summary>
public class MemoryEntry
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; }
}