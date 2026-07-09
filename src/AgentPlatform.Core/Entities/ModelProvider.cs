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

    public ModelProvider? ModelProvider { get; set; }
}
