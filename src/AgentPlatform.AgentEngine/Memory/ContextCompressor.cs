using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Memory;

/// <summary>
/// 上下文压缩策略
/// </summary>
public enum CompressionStrategy
{
    /// <summary>基于 LLM 摘要的智能压缩（默认）</summary>
    Summarize,
    /// <summary>滑动窗口截断（从最早的消息开始丢弃）</summary>
    SlidingWindow,
    /// <summary>混合模式：先用滑动窗口截断，再对剩余消息做摘要</summary>
    Hybrid
}

/// <summary>
/// 智能上下文压缩器 — 基于 Microsoft Agent Framework 的内存和持久性模式（步骤 4）。
///
/// 功能：
/// 1. Summarize 模式：将最早的消息批次提交给 LLM 生成摘要，保留最近的原始消息
/// 2. SlidingWindow 模式：按 Token 预算从最早消息开始丢弃
/// 3. Hybrid 模式：先滑动窗口缩减，再对旧消息做摘要
///
/// 设计依据（agent-framework.pdf）：
/// - 步骤 4（内存和持久性）：使用 IAIContextProvider 管理聊天历史记录和内存
/// - ChatHistoryProvider：管理对话历史记录存储和检索
/// - InMemoryChatHistoryProvider：默认的内存聊天历史记录提供程序
/// - 上下文压缩属于 AIContextProvider 层的职责
/// </summary>
public class ContextCompressor
{
    private readonly IChatClient _summarizerClient;
    private readonly int _maxTokens;
    private readonly CompressionStrategy _strategy;
    private readonly ILogger<ContextCompressor> _logger;

    /// <summary>系统保留的最小消息数（System + 最近 N 轮）</summary>
    private const int MinRecentMessages = 4;

    /// <summary>摘要消息的 Token 预算上限</summary>
    private const int SummaryTokenBudget = 512;

    public ContextCompressor(
        IChatClient summarizerClient,
        int maxTokens = 4096,
        CompressionStrategy strategy = CompressionStrategy.Summarize,
        ILogger<ContextCompressor>? logger = null)
    {
        _summarizerClient = summarizerClient ?? throw new ArgumentNullException(nameof(summarizerClient));
        _maxTokens = maxTokens > 0 ? maxTokens : throw new ArgumentOutOfRangeException(nameof(maxTokens));
        _strategy = strategy;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 压缩消息列表，确保总 Token 数不超过预算
    /// </summary>
    /// <param name="messages">原始消息列表（时间正序）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>压缩后的消息列表</returns>
    public async Task<List<ChatMessage>> CompressAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken ct = default)
    {
        if (messages.Count == 0)
            return new List<ChatMessage>();

        // 计算总 Token 数
        var totalTokens = EstimateTokenCount(messages);
        if (totalTokens <= _maxTokens)
        {
            _logger.LogDebug("No compression needed: {TokenCount} <= {MaxTokens}", totalTokens, _maxTokens);
            return new List<ChatMessage>(messages);
        }

        _logger.LogInformation(
            "Compressing context: {Count} messages, {TokenCount} tokens (budget: {MaxTokens})",
            messages.Count, totalTokens, _maxTokens);

        return _strategy switch
        {
            CompressionStrategy.Summarize => await SummarizeCompressionAsync(messages, ct),
            CompressionStrategy.SlidingWindow => SlidingWindowCompression(messages),
            CompressionStrategy.Hybrid => await HybridCompressionAsync(messages, ct),
            _ => SlidingWindowCompression(messages)
        };
    }

    // ══════════════════════════════════════════════════════════════
    //  Summarize 压缩模式
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 基于 LLM 摘要的压缩：
    /// - 保留最近的 MinRecentMessages 条原始消息
    /// - 将更早的消息合并提交给 LLM 生成摘要
    /// - 用摘要消息替换旧消息
    /// </summary>
    private async Task<List<ChatMessage>> SummarizeCompressionAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        if (messages.Count <= MinRecentMessages)
            return SlidingWindowCompression(messages);

        // 分离 System 消息（始终保留）
        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();
        var nonSystemMessages = messages.Where(m => m.Role != ChatRole.System).ToList();

        // 保留最近的消息
        var recentMessages = nonSystemMessages
            .Skip(nonSystemMessages.Count - MinRecentMessages)
            .ToList();

        // 旧消息需要摘要
        var oldMessages = nonSystemMessages
            .Take(nonSystemMessages.Count - MinRecentMessages)
            .ToList();

        if (oldMessages.Count == 0)
        {
            return new List<ChatMessage>(messages);
        }

        // 生成摘要
        var summary = await SummarizeMessagesAsync(oldMessages, ct);

        // 构建压缩后的消息列表：System + 摘要 + 最近消息
        var compressed = new List<ChatMessage>();
        compressed.AddRange(systemMessages);

        if (!string.IsNullOrEmpty(summary))
        {
            compressed.Add(new ChatMessage(ChatRole.System,
                $"[先前对话摘要] {summary}"));
        }

        compressed.AddRange(recentMessages);

        _logger.LogInformation(
            "Summarize compression: {OriginalCount} → {CompressedCount} messages",
            messages.Count, compressed.Count);

        return compressed;
    }

    // ══════════════════════════════════════════════════════════════
    //  SlidingWindow 压缩模式
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 滑动窗口截断：从最早的非 System 消息开始丢弃，直到 Token 数在预算内
    /// </summary>
    private List<ChatMessage> SlidingWindowCompression(IReadOnlyList<ChatMessage> messages)
    {
        // System 消息始终保留
        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();
        var others = messages.Where(m => m.Role != ChatRole.System).ToList();

        // 确保至少保留 MinRecentMessages 条非 System 消息
        while (others.Count > MinRecentMessages &&
               EstimateTokenCount(systemMessages) + EstimateTokenCount(others) > _maxTokens)
        {
            others.RemoveAt(0); // 丢弃最早的非 System 消息
        }

        var result = new List<ChatMessage>();
        result.AddRange(systemMessages);
        result.AddRange(others);

        _logger.LogInformation(
            "SlidingWindow compression: {OriginalCount} → {CompressedCount} messages (dropped {Dropped})",
            messages.Count, result.Count, messages.Count - result.Count);

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    //  Hybrid 压缩模式
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 混合模式：先用滑动窗口快速缩减，再对旧消息做 LLM 摘要
    /// </summary>
    private async Task<List<ChatMessage>> HybridCompressionAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        // 第一步：确保消息条数不超过阈值（保留至少 2× MinRecentMessages 条）
        var maxMessages = Math.Max(MinRecentMessages * 2, 20);
        var truncated = messages.Count > maxMessages
            ? SlidingWindowCompression(messages.Take(messages.Count - MinRecentMessages).ToList())
                .Concat(messages.Skip(messages.Count - MinRecentMessages))
                .ToList()
            : new List<ChatMessage>(messages);

        // 第二步：如果 Token 仍然超限，使用 Summarize
        if (EstimateTokenCount(truncated) > _maxTokens)
        {
            return await SummarizeCompressionAsync(truncated, ct);
        }

        return truncated;
    }

    // ══════════════════════════════════════════════════════════════
    //  LLM 摘要
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 调用 LLM 对消息列表生成简洁摘要
    /// </summary>
    private async Task<string> SummarizeMessagesAsync(
        IReadOnlyList<ChatMessage> messages, CancellationToken ct)
    {
        try
        {
            var conversationText = string.Join("\n",
                messages.Select(m => $"[{m.Role}]: {m.Text ?? "(工具调用)"}"));

            var summaryMessages = new List<ChatMessage>
            {
                new(ChatRole.System, "你是一个对话摘要助手。请为以下对话生成简洁的中文摘要（200字以内），" +
                    "保留关键事实、用户意图、以及 Agent 的决策和工具调用结果。"),
                new(ChatRole.User, $"请摘要以下对话：\n\n{conversationText}")
            };

            var response = await _summarizerClient.GetResponseAsync(summaryMessages, new ChatOptions
            {
                Temperature = 0.3f,
                MaxOutputTokens = SummaryTokenBudget
            }, ct);

            var summary = response.Messages.LastOrDefault()?.Text ?? string.Empty;

            _logger.LogDebug("Generated summary ({Length} chars) from {Count} messages",
                summary.Length, messages.Count);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM summarization failed, falling back to sliding window");
            return string.Empty;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Token 估算
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 估算消息列表的总 Token 数
    /// 中文 ≈ 1.5 字符/Token，英文 ≈ 4 字符/Token
    /// </summary>
    public static int EstimateTokenCount(IReadOnlyList<ChatMessage> messages)
    {
        int total = 0;
        foreach (var msg in messages)
        {
            total += EstimateTokenCount(msg.Text ?? string.Empty);
            // 每条消息的 Role 和格式化开销
            total += 4;
        }
        return total;
    }

    /// <summary>
    /// 估算文本的 Token 数
    /// </summary>
    public static int EstimateTokenCount(string text)
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

        return (int)(chineseChars / 1.5 + otherChars / 4.0) + 1;
    }
}
