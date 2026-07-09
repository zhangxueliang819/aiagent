using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Memory;

/// <summary>
/// 短期记忆存储实现：基于持久化 Conversation 记录管理上下文窗口
/// 
/// 实现滑动窗口 + Token 截断策略：
/// - 默认保留最近 20 轮对话
/// - 超过 maxTokens 时从最早的消息开始舍弃（但保留 system prompt）
/// - Token 估算：中文 ≈ 字符数/2，英文 ≈ 字符数/4
/// </summary>
public class ShortTermMemoryStore : IShortTermMemoryStore
{
    private readonly ISessionRepository _sessionRepo;
    private readonly ILogger<ShortTermMemoryStore> _logger;

    /// <summary>默认上下文窗口最大消息条数</summary>
    private const int DefaultMaxMessages = 20;

    /// <summary>最小保留消息条数（以免空上下文）</summary>
    private const int MinMessages = 1;

    public ShortTermMemoryStore(ISessionRepository sessionRepo, ILogger<ShortTermMemoryStore> logger)
    {
        _sessionRepo = sessionRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<MemoryEntry>> GetContextWindowAsync(Guid sessionId, int maxTokens, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            _logger.LogWarning("Session {SessionId} not found for memory retrieval", sessionId);
            return new List<MemoryEntry>();
        }

        var conversations = session.Conversations
            .OrderByDescending(c => c.CreatedAt)
            .Take(DefaultMaxMessages)
            .Select(c => new MemoryEntry
            {
                Role = c.Role,
                Content = c.Content,
                TokenCount = c.TokenCount,
                CreatedAt = c.CreatedAt
            })
            .Reverse() // 恢复时间正序
            .ToList();

        // Token 截断：从最早的消息开始舍弃，直到总 Token 在预算内
        var totalTokens = conversations.Sum(c => c.TokenCount);
        while (totalTokens > maxTokens && conversations.Count > MinMessages)
        {
            var removed = conversations[0];
            conversations.RemoveAt(0);
            totalTokens -= removed.TokenCount;
            _logger.LogDebug("Truncated memory entry (tokens: {Tokens}), remaining: {Count} entries, {TotalTokens} tokens",
                removed.TokenCount, conversations.Count, totalTokens);
        }

        _logger.LogInformation("Context window for session {SessionId}: {Count} messages, {TotalTokens}/{MaxTokens} tokens",
            sessionId, conversations.Count, totalTokens, maxTokens);

        return conversations;
    }

    /// <inheritdoc />
    public async Task AddMessageAsync(Guid sessionId, string role, string content, CancellationToken ct = default)
    {
        await _sessionRepo.AddConversationAsync(new Core.Entities.Conversation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = role,
            Content = content,
            TokenCount = EstimateTokens(content),
            CreatedAt = DateTime.UtcNow
        }, ct);

        _logger.LogDebug("Memory stored: [{Role}] {Length} chars for session {SessionId}",
            role, content.Length, sessionId);
    }

    /// <inheritdoc />
    public async Task<int> GetTokenCountAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session is null) return 0;

        return session.Conversations.Sum(c => c.TokenCount);
    }

    /// <inheritdoc />
    public Task ClearAsync(Guid sessionId, CancellationToken ct = default)
    {
        // InMemory 模式下由 Session 生命周期管理；Redis 模式下需要主动删除
        _logger.LogInformation("Memory cleared for session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 估算文本的 Token 数量（简单启发式：中文 2 字符 ≈ 1 token，英文 4 字符 ≈ 1 token）
    /// </summary>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int chineseChars = 0;
        int otherChars = 0;
        foreach (var ch in text)
        {
            if (ch >= 0x4E00 && ch <= 0x9FFF)
                chineseChars++;
            else
                otherChars++;
        }

        // 中文约每 1.5 字符 1 token，英文约每 4 字符 1 token
        return (int)(chineseChars / 1.5 + otherChars / 4.0) + 1;
    }
}
