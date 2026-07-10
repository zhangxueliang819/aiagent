using System.Collections.Concurrent;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.ModelProviders.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.Application.Services;

/// <summary>
/// 模型路由器（V2.0）：返回 MEAI IChatClient，支持负载均衡策略。
/// </summary>
public class ModelRouter
{
    private readonly IModelProviderRepository _repo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly RateLimiter _rateLimiter;
    private readonly ModelMetricsCollector _metricsCollector;

    private readonly ConcurrentDictionary<Guid, CachedProvider> _providers = new();
    private readonly ConcurrentDictionary<Guid, int> _roundRobinCounters = new();

    public ModelRouter(
        IModelProviderRepository repo,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        RateLimiter rateLimiter,
        ModelMetricsCollector metricsCollector)
    {
        _repo = repo;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _rateLimiter = rateLimiter;
        _metricsCollector = metricsCollector;
    }

    /// <summary>
    /// 根据 Agent 解析 IChatClient（直接使用 Agent 配置的 ModelEndpointId）
    /// </summary>
    public async Task<IChatClient?> ResolveAsync(Agent agent, CancellationToken ct = default)
    {
        if (agent.ModelEndpointId is null)
            return null;

        var endpointId = agent.ModelEndpointId.Value;
        var endpoint = await GetEndpointAsync(endpointId, ct);
        if (endpoint is null)
            return null;

        return CreateProvider(endpoint);
    }

    /// <summary>
    /// 根据负载均衡策略从 Provider 中选取最优端点
    /// </summary>
    public async Task<(IChatClient Provider, ModelEndpoint Endpoint)?> ResolveWithLoadBalancingAsync(
        ModelProvider provider, int estimatedTokens = 0, CancellationToken ct = default)
    {
        var endpoints = await _repo.GetEnabledEndpointsByProviderAsync(provider.Id, ct);
        if (endpoints.Count == 0)
            return null;

        var available = endpoints.Where(e =>
        {
            if (e.RpmLimit > 0 && _rateLimiter.IsRateLimited(e.Id, e.RpmLimit, e.TpmLimit))
                return false;
            return true;
        }).ToList();

        if (available.Count == 0)
            available = endpoints;

        var selected = provider.RoutingStrategy switch
        {
            "Weighted" => SelectWeighted(available),
            "LeastConnections" => SelectLeastConnections(available),
            _ => SelectRoundRobin(available)
        };

        var (allowed, _) = _rateLimiter.CheckAndRecord(selected.Id, estimatedTokens, selected.RpmLimit, selected.TpmLimit);
        if (!allowed)
        {
            selected = SelectRoundRobin(available);
            _rateLimiter.CheckAndRecord(selected.Id, estimatedTokens, selected.RpmLimit, selected.TpmLimit);
        }

        var llmProvider = CreateProvider(selected);
        return llmProvider is null ? null : (llmProvider, selected);
    }

    private ModelEndpoint SelectRoundRobin(List<ModelEndpoint> endpoints)
    {
        var providerId = endpoints[0].ModelProviderId;
        var counter = _roundRobinCounters.AddOrUpdate(providerId, 1, (_, v) => v + 1);
        return endpoints[counter % endpoints.Count];
    }

    private static ModelEndpoint SelectWeighted(List<ModelEndpoint> endpoints)
    {
        var totalWeight = endpoints.Sum(e => e.Weight);
        if (totalWeight <= 0) return endpoints[0];

        var random = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        foreach (var endpoint in endpoints)
        {
            cumulative += endpoint.Weight;
            if (random < cumulative)
                return endpoint;
        }
        return endpoints[^1];
    }

    private ModelEndpoint SelectLeastConnections(List<ModelEndpoint> endpoints)
    {
        ModelEndpoint? best = null;
        var minConnections = int.MaxValue;

        foreach (var endpoint in endpoints)
        {
            var metrics = _metricsCollector.GetMetrics(endpoint.Id);
            var active = metrics?.ActiveRequests ?? 0;
            if (active < minConnections)
            {
                minConnections = active;
                best = endpoint;
            }
        }

        return best ?? endpoints[0];
    }

    public void InvalidateCache(Guid endpointId)
    {
        _providers.TryRemove(endpointId, out _);
        _rateLimiter.Reset(endpointId);
        _metricsCollector.Reset(endpointId);
    }

    private async Task<ModelEndpoint?> GetEndpointAsync(Guid endpointId, CancellationToken ct)
    {
        if (_providers.TryGetValue(endpointId, out var cached) && !cached.IsExpired)
            return cached.Endpoint;

        var endpoint = await _repo.GetEndpointWithProviderAsync(endpointId, ct);
        if (endpoint is null) return null;

        _providers[endpointId] = new CachedProvider(endpoint, null!, DateTime.UtcNow.AddHours(1));
        return endpoint;
    }

    private IChatClient? CreateProvider(ModelEndpoint endpoint)
    {
        if (!endpoint.IsEnabled)
            throw new InvalidOperationException($"Model endpoint '{endpoint.ModelName}' is disabled.");

        if (endpoint.ModelProvider is null)
            return null;

        var httpClient = _httpClientFactory.CreateClient($"llm-{endpoint.Id}");

        return new OpenAIProvider(
            endpoint.ModelProvider.ApiBaseUrl,
            endpoint.ModelProvider.EncryptedApiKey,
            endpoint.ModelId,
            httpClient,
            _loggerFactory.CreateLogger<OpenAIProvider>());
    }

    private record CachedProvider(ModelEndpoint Endpoint, IChatClient Provider, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
