using AgentPlatform.Core.Entities;

namespace AgentPlatform.Application.DTOs;

public record AgentDto(
    Guid Id, string Name, string Description, string SystemPrompt,
    string ModelId, Guid? ModelEndpointId, string? ModelEndpointName,
    string Status, string Version,
    string CreatedBy, DateTime CreatedAt, DateTime UpdatedAt,
    double? Temperature, int? MaxTokens, double? TopP,
    List<AgentConfigurationDto> Configurations, List<AgentSkillDto> Skills);

public record AgentConfigurationDto(Guid Id, string Key, string Value, string ValueType);
public record AgentSkillDto(Guid Id, Guid SkillId, int Priority, bool IsEnabled);

public record CreateAgentRequest(
    string Name, string Description, string SystemPrompt,
    string ModelId, Guid? ModelEndpointId, string CreatedBy,
    double? Temperature = null, int? MaxTokens = null, double? TopP = null);

public record UpdateAgentRequest(
    string? Name, string? Description, string? SystemPrompt,
    string? ModelId, Guid? ModelEndpointId, string? Status,
    double? Temperature = null, int? MaxTokens = null, double? TopP = null);

public record ModelProviderDto(
    Guid Id, string Name, string ProviderType, string ApiBaseUrl,
    bool IsEnabled, string RoutingStrategy, DateTime CreatedAt, List<ModelEndpointDto> Endpoints);

public record ModelEndpointDto(Guid Id, string ModelId, string ModelName, int MaxTokens, bool IsEnabled,
    int Weight, int RpmLimit, int TpmLimit);

public record ModelMetricsDto(string EndpointId, string ModelName, int ActiveRequests,
    long TotalRequests, long ErrorCount, double AvgLatencyMs, bool IsRateLimited);

public record CreateModelProviderRequest(
    string Name, string ProviderType, string ApiBaseUrl, string ApiKey,
    string RoutingStrategy = "RoundRobin");

public record CreateModelEndpointRequest(
    string ModelId, string ModelName, int MaxTokens = 4096,
    decimal InputPricePer1K = 0, decimal OutputPricePer1K = 0,
    int Weight = 1, int RpmLimit = 0, int TpmLimit = 0);

public record SkillDto(
    Guid Id, string Name, string Description, string Type,
    string Implementation, string InputSchema, bool IsEnabled,
    string StorageType, string? StoragePath, string? OriginalFileName, string? FileManifest,
    DateTime CreatedAt, DateTime UpdatedAt);

public record CreateSkillRequest(
    string Name, string Description, string Type,
    string Implementation, string InputSchema,
    string? StorageType = null);

public record UpdateSkillRequest(
    string? Name, string? Description, string? Type,
    string? Implementation, string? InputSchema,
    bool? IsEnabled);

/// <summary>技能包内文件项</summary>
public record SkillFileItem(string Path, long Size, DateTime LastModified);

/// <summary>上传技能包响应</summary>
public record SkillUploadResponse(SkillDto Skill, List<SkillFileItem> Files);

public record SessionDto(Guid Id, string Title, Guid AgentId, string Status,
    DateTime CreatedAt, List<ConversationDto> Conversations);

public record ConversationDto(Guid Id, string Role, string Content, int TokenCount, DateTime CreatedAt);

public record UsageRecordDto(Guid Id, string ModelId, int InputTokens, int OutputTokens, decimal Cost, DateTime CreatedAt);

public record UsageSummaryDto(int TotalRequests, int TotalInputTokens, int TotalOutputTokens, decimal TotalCost, int AgentCount);

public record UsageDailyDto(string Date, int RequestCount, int InputTokens, int OutputTokens, decimal Cost);

public record UsageAgentSummaryDto(Guid AgentId, int RequestCount, int TotalTokens, decimal TotalCost);

public record ApiResponse<T>(bool Success, string Message, T? Data);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

// Agent 状态切换
public record ChangeAgentStatusRequest(string Status);

// Agent 克隆
public record CloneAgentRequest(string Name, string? CreatedBy);

// Agent 版本
public record AgentVersionDto(int Id, int VersionNumber, string ChangeSummary, string ChangedBy, DateTime CreatedAt);
