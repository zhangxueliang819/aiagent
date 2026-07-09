using System.Collections.Concurrent;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.ModelProviders.OpenAI;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.Application.Services;

/// <summary>
/// 模型路由器：根据 Agent 配置的 ModelEndpointId 动态解析对应的 IModelProvider 实例。
/// 
/// 解析链路：Agent → ModelEndpoint → ModelProvider → IModelProvider (OpenAIProvider)
/// 
/// 内置缓存：按 ModelEndpointId 缓存 Provider 实例，避免重复创建 HttpClient。
/// </summary>
public class ModelRouter
{
    private readonly IModelProviderRepository _repo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>缓存的 Provider 实例，key 为 ModelEndpointId</summary>
    private readonly ConcurrentDictionary<Guid, CachedProvider> _providers = new();

    public ModelRouter(
        IModelProviderRepository repo,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _repo = repo;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 根据 Agent 解析 IModelProvider。
    /// 如果 Agent 未配置 ModelEndpointId，返回 null（调用方应回退到模拟 LLM）。
    /// </summary>
    public async Task<IModelProvider?> ResolveAsync(Agent agent, CancellationToken ct = default)
    {
        if (agent.ModelEndpointId is null)
            return null;

        var endpointId = agent.ModelEndpointId.Value;

        // 尝试缓存命中
        if (_providers.TryGetValue(endpointId, out var cached))
        {
            if (!cached.IsExpired)
                return cached.Provider;
            _providers.TryRemove(endpointId, out _);
        }

        // 从数据库加载 ModelEndpoint + ModelProvider
        var endpoint = await _repo.GetEndpointWithProviderAsync(endpointId, ct);
        if (endpoint is null || endpoint.ModelProvider is null)
            return null;

        if (!endpoint.IsEnabled)
        {
            throw new InvalidOperationException(
                $"Model endpoint '{endpoint.ModelName}' is disabled.");
        }

        // 创建 HttpClient（命名客户端，便于日志和诊断）
        var httpClient = _httpClientFactory.CreateClient($"llm-{endpoint.Id}");

        var provider = new OpenAIProvider(
            endpoint.ModelProvider.ApiBaseUrl,
            endpoint.ModelProvider.EncryptedApiKey,
            endpoint.ModelId,
            httpClient,
            _loggerFactory.CreateLogger<OpenAIProvider>());

        // 缓存（60 分钟过期，API key 变更后会自然更新）
        _providers[endpointId] = new CachedProvider(provider, DateTime.UtcNow.AddHours(1));

        return provider;
    }

    /// <summary>
    /// 清除指定端点的缓存（用于 API Key 变更后刷新）
    /// </summary>
    public void InvalidateCache(Guid endpointId)
    {
        _providers.TryRemove(endpointId, out _);
    }

    private record CachedProvider(IModelProvider Provider, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
