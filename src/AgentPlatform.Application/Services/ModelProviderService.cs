using AgentPlatform.Application.DTOs;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class ModelProviderService
{
    private readonly IModelProviderRepository _repository;

    public ModelProviderService(IModelProviderRepository repository) => _repository = repository;

    public async Task<List<ModelProviderDto>> GetAllAsync(CancellationToken ct = default)
    {
        var providers = await _repository.GetAllAsync(ct);
        return providers.Select(Map).ToList();
    }

    public async Task<ModelProviderDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _repository.GetByIdAsync(id, ct);
        return p is null ? null : Map(p);
    }

    public async Task<ModelProviderDto> CreateAsync(CreateModelProviderRequest request, CancellationToken ct = default)
    {
        var provider = new ModelProvider
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ProviderType = request.ProviderType,
            ApiBaseUrl = request.ApiBaseUrl,
            EncryptedApiKey = request.ApiKey, // TODO: encrypt
            RoutingStrategy = request.RoutingStrategy,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _repository.AddAsync(provider, ct);
        return Map(result);
    }

    public async Task<ModelProviderDto?> UpdateAsync(Guid id, CreateModelProviderRequest request, CancellationToken ct = default)
    {
        var p = await _repository.GetByIdAsync(id, ct);
        if (p is null) return null;
        p.Name = request.Name;
        p.ProviderType = request.ProviderType;
        p.ApiBaseUrl = request.ApiBaseUrl;
        p.EncryptedApiKey = request.ApiKey;
        return Map(await _repository.UpdateAsync(p, ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return true;
    }

    // ─── Endpoint Management ────────────────────────────────────────

    public async Task<ModelEndpointDto> AddEndpointAsync(Guid providerId, CreateModelEndpointRequest request, CancellationToken ct = default)
    {
        _ = await _repository.GetByIdAsync(providerId, ct)
            ?? throw new InvalidOperationException("Model provider not found");

        var endpoint = new ModelEndpoint
        {
            Id = Guid.NewGuid(),
            ModelProviderId = providerId,
            ModelId = request.ModelId,
            ModelName = request.ModelName,
            MaxTokens = request.MaxTokens,
            InputPricePer1K = request.InputPricePer1K,
            OutputPricePer1K = request.OutputPricePer1K,
            IsEnabled = true,
            Weight = request.Weight,
            RpmLimit = request.RpmLimit,
            TpmLimit = request.TpmLimit
        };

        var result = await _repository.AddEndpointAsync(endpoint, ct);

        return new ModelEndpointDto(result.Id, result.ModelId, result.ModelName, result.MaxTokens, result.IsEnabled,
            result.Weight, result.RpmLimit, result.TpmLimit);
    }

    public async Task<bool> DeleteEndpointAsync(Guid endpointId, CancellationToken ct = default)
    {
        var endpoint = await _repository.GetEndpointWithProviderAsync(endpointId, ct)
            ?? throw new InvalidOperationException("Model endpoint not found");

        await _repository.DeleteEndpointAsync(endpoint, ct);

        return true;
    }

    private static ModelProviderDto Map(ModelProvider p) => new(
        p.Id, p.Name, p.ProviderType, p.ApiBaseUrl, p.IsEnabled, p.RoutingStrategy, p.CreatedAt,
        p.Endpoints.Select(e => new ModelEndpointDto(e.Id, e.ModelId, e.ModelName, e.MaxTokens, e.IsEnabled,
            e.Weight, e.RpmLimit, e.TpmLimit)).ToList());
}
