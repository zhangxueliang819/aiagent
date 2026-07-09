using System.Collections.Concurrent;

namespace AgentPlatform.Application.Services;

/// <summary>
/// 滑动窗口速率限制器（端点级 RPM/TPM）
/// 使用时间窗口 + 请求计数实现，线程安全
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<Guid, EndpointRateState> _states = new();

    /// <summary>
    /// 检查指定端点是否允许请求，并记录
    /// </summary>
    /// <returns>(allowed, retryAfterMs)</returns>
    public (bool Allowed, int RetryAfterMs) CheckAndRecord(Guid endpointId, int estimatedTokens, int rpmLimit, int tpmLimit)
    {
        var now = DateTime.UtcNow;
        var state = _states.GetOrAdd(endpointId, _ => new EndpointRateState());

        lock (state.Lock)
        {
            // 清理过期记录（60秒窗口）
            var windowStart = now.AddSeconds(-60);
            while (state.RequestTimestamps.Count > 0 && state.RequestTimestamps.Peek().Timestamp < windowStart)
            {
                var removed = state.RequestTimestamps.Dequeue();
                state.TokensInWindow -= removed.EstimatedTokens;
                state.RequestCount--;
            }

            // 检查 RPM 限制
            if (rpmLimit > 0 && state.RequestCount >= rpmLimit)
            {
                var oldestInWindow = state.RequestTimestamps.Peek();
                var retryAfter = (int)(oldestInWindow.Timestamp.AddSeconds(60) - now).TotalMilliseconds;
                return (false, Math.Max(retryAfter, 100));
            }

            // 检查 TPM 限制
            if (tpmLimit > 0 && state.TokensInWindow + estimatedTokens > tpmLimit)
            {
                return (false, 1000); // 至少等1秒
            }

            // 记录请求
            state.RequestTimestamps.Enqueue(new RateRecord(now, estimatedTokens));
            state.RequestCount++;
            state.TokensInWindow += estimatedTokens;
            return (true, 0);
        }
    }

    /// <summary>
    /// 清除指定端点的速率状态（端点配置变更时调用）
    /// </summary>
    public void Reset(Guid endpointId)
    {
        _states.TryRemove(endpointId, out _);
    }

    /// <summary>
    /// 获取当前是否被限流
    /// </summary>
    public bool IsRateLimited(Guid endpointId, int rpmLimit, int tpmLimit)
    {
        if (!_states.TryGetValue(endpointId, out var state))
            return false;

        lock (state.Lock)
        {
            if (rpmLimit > 0 && state.RequestCount >= rpmLimit)
                return true;
            if (tpmLimit > 0 && state.TokensInWindow >= tpmLimit)
                return true;
            return false;
        }
    }

    private class EndpointRateState
    {
        public readonly object Lock = new();
        public readonly Queue<RateRecord> RequestTimestamps = new();
        public int RequestCount;
        public int TokensInWindow;
    }

    private record RateRecord(DateTime Timestamp, int EstimatedTokens);
}
