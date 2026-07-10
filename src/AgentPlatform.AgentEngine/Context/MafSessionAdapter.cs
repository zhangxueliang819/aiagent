using System.Text.Json;
using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.AgentEngine.Context;

/// <summary>
/// Session 双态适配器：桥接 Session 实体（持久化）与 MAF Agent 会话状态。
///
/// MAF ChatClientAgentSession 由 ChatClientAgent.CreateSessionAsync() 创建，
/// 其序列化通过 Session.SerializeState() / Session.DeserializeState() 实现。
/// 本适配器负责 Session 实体 ↔ 运行时状态的 JSON 序列化/反序列化。
/// </summary>
public class MafSessionAdapter
{
    private readonly ISessionRepository _sessionRepo;
    private readonly ILogger<MafSessionAdapter> _logger;

    public MafSessionAdapter(ISessionRepository sessionRepo, ILogger<MafSessionAdapter> logger)
    {
        _sessionRepo = sessionRepo;
        _logger = logger;
    }

    /// <summary>
    /// 从 Session 实体恢复运行时状态
    /// </summary>
    public Dictionary<string, object?> LoadState(Session session)
    {
        if (string.IsNullOrEmpty(session.SerializedState))
            return new();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(session.SerializedState)
                ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize state for session {SessionId}", session.Id);
            return new();
        }
    }

    /// <summary>
    /// 将运行时状态序列化到 Session 实体
    /// </summary>
    public void SaveState(Session session, Dictionary<string, object?> state)
    {
        session.SerializedState = JsonSerializer.Serialize(state);
        session.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新单个状态键
    /// </summary>
    public async Task SetStateAsync(Guid sessionId, string key, object? value, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session is null) return;

        var state = LoadState(session);
        state[key] = value;
        SaveState(session, state);
        await _sessionRepo.UpdateAsync(session, ct);
    }

    /// <summary>
    /// 读取单个状态键
    /// </summary>
    public async Task<T?> GetStateAsync<T>(Guid sessionId, string key, CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session is null) return default;

        var state = LoadState(session);
        if (state.TryGetValue(key, out var value) && value is JsonElement je)
            return je.Deserialize<T>();

        return value is T typed ? typed : default;
    }

    /// <summary>
    /// 从 Session 实体反序列化会话状态字符串
    /// </summary>
    public static string? GetSerializedState(Session entity)
        => entity.SerializedState;

    /// <summary>
    /// 将序列化状态字符串存入 Session 实体
    /// </summary>
    public static void SetSerializedState(Session entity, string? serializedState)
    {
        entity.SerializedState = serializedState;
        entity.UpdatedAt = DateTime.UtcNow;
    }
}
