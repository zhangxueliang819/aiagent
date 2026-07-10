namespace AgentPlatform.Core.Entities;

public class ModelProvider
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty; // OpenAI, AzureOpenAI
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string EncryptedApiKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>负载均衡策略：RoundRobin / Weighted / LeastConnections</summary>
    public string RoutingStrategy { get; set; } = "RoundRobin";

    /// <summary>
    /// MAF IChatClient 工厂类型的程序集限定名（如 "MyApp.Providers.MyOpenAIClientFactory, MyApp"）
    /// 用于 MAF 集成时通过反射创建 IChatClient
    /// </summary>
    public string? ClientTypeAssembly { get; set; }

    public List<ModelEndpoint> Endpoints { get; set; } = new();
}

public class ModelEndpoint
{
    public Guid Id { get; set; }
    public Guid ModelProviderId { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 4096;
    public decimal InputPricePer1K { get; set; }
    public decimal OutputPricePer1K { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>负载均衡权重（Weighted 策略使用）</summary>
    public int Weight { get; set; } = 1;

    /// <summary>每分钟最大请求数，0 表示不限</summary>
    public int RpmLimit { get; set; }

    /// <summary>每分钟最大 Token 数，0 表示不限</summary>
    public int TpmLimit { get; set; }

    public ModelProvider? ModelProvider { get; set; }
}
