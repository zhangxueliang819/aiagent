using System.Collections.Concurrent;

namespace AgentPlatform.Application.Services;

/// <summary>
/// 模型端点性能指标收集器
/// 跟踪每个端点的请求数、错误数、延迟等指标
/// </summary>
public class ModelMetricsCollector
{
    private readonly ConcurrentDictionary<Guid, EndpointMetrics> _metrics = new();

    /// <summary>记录请求开始，返回请求 ID</summary>
    public Guid RecordRequestStart(Guid endpointId)
    {
        var metrics = _metrics.GetOrAdd(endpointId, _ => new EndpointMetrics());
        lock (metrics.Lock)
        {
            metrics.ActiveRequests++;
            metrics.TotalRequests++;
            return Guid.NewGuid();
        }
    }

    /// <summary>记录请求完成</summary>
    public void RecordRequestEnd(Guid endpointId, long elapsedMs, bool isError)
    {
        if (!_metrics.TryGetValue(endpointId, out var metrics)) return;

        lock (metrics.Lock)
        {
            metrics.ActiveRequests--;
            if (isError)
                metrics.ErrorCount++;

            // 指数移动平均延迟（EWMA）
            const double alpha = 0.3;
            metrics.AvgLatencyMs = metrics.AvgLatencyMs == 0
                ? elapsedMs
                : alpha * elapsedMs + (1 - alpha) * metrics.AvgLatencyMs;
        }
    }

    /// <summary>获取端点当前指标</summary>
    public EndpointMetricsSnapshot? GetMetrics(Guid endpointId)
    {
        if (!_metrics.TryGetValue(endpointId, out var metrics)) return null;

        lock (metrics.Lock)
        {
            return new EndpointMetricsSnapshot(
                endpointId, metrics.ActiveRequests, metrics.TotalRequests,
                metrics.ErrorCount, metrics.AvgLatencyMs);
        }
    }

    /// <summary>获取所有端点的指标快照</summary>
    public List<EndpointMetricsSnapshot> GetAllMetrics()
    {
        var result = new List<EndpointMetricsSnapshot>();
        foreach (var kvp in _metrics)
        {
            lock (kvp.Value.Lock)
            {
                result.Add(new EndpointMetricsSnapshot(
                    kvp.Key, kvp.Value.ActiveRequests, kvp.Value.TotalRequests,
                    kvp.Value.ErrorCount, kvp.Value.AvgLatencyMs));
            }
        }
        return result;
    }

    /// <summary>清除指定端点的指标</summary>
    public void Reset(Guid endpointId)
    {
        _metrics.TryRemove(endpointId, out _);
    }

    private class EndpointMetrics
    {
        public readonly object Lock = new();
        public int ActiveRequests;
        public long TotalRequests;
        public long ErrorCount;
        public double AvgLatencyMs;
    }
}

public record EndpointMetricsSnapshot(
    Guid EndpointId, int ActiveRequests, long TotalRequests,
    long ErrorCount, double AvgLatencyMs);
