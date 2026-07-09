using AgentPlatform.Core.Entities;

namespace AgentPlatform.Application.DTOs;

public record AgentDto(
    Guid Id, string Name, string Description, string SystemPrompt,
    string ModelId, Guid? ModelEndpointId, string? ModelEndpointName,
    string Status, string Version,
    string CreatedBy, DateTime CreatedAt, DateTime UpdatedAt,
    List<AgentConfigurationDto> Configurations, List<AgentSkillDto> Skills);

public record AgentConfigurationDto(Guid Id, string Key, string Value, string ValueType);
public record AgentSkillDto(Guid Id, Guid SkillId, int Priority, bool IsEnabled);

public record CreateAgentRequest(
    string Name, string Description, string SystemPrompt,
    string ModelId, Guid? ModelEndpointId, string CreatedBy);

public record UpdateAgentRequest(
    string? Name, string? Description, string? SystemPrompt,
    string? ModelId, Guid? ModelEndpointId, string? Status);

public record ModelProviderDto(
    Guid Id, string Name, string ProviderType, string ApiBaseUrl,
    bool IsEnabled, DateTime CreatedAt, List<ModelEndpointDto> Endpoints);

public record ModelEndpointDto(Guid Id, string ModelId, string ModelName, int MaxTokens, bool IsEnabled);

public record CreateModelProviderRequest(
    string Name, string ProviderType, string ApiBaseUrl, string ApiKey);

public record CreateModelEndpointRequest(
    string ModelId, string ModelName, int MaxTokens = 4096,
    decimal InputPricePer1K = 0, decimal OutputPricePer1K = 0);

public record SkillDto(Guid Id, string Name, string Description, string Type,
    string InputSchema, bool IsEnabled, DateTime CreatedAt);

public record CreateSkillRequest(string Name, string Description, string Type, string Implementation, string InputSchema);

public record SessionDto(Guid Id, string Title, Guid AgentId, string Status,
    DateTime CreatedAt, List<ConversationDto> Conversations);

public record ConversationDto(Guid Id, string Role, string Content, int TokenCount, DateTime CreatedAt);

public record UsageRecordDto(Guid Id, string ModelId, int InputTokens, int OutputTokens, decimal Cost, DateTime CreatedAt);

public record ApiResponse<T>(bool Success, string Message, T? Data);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
