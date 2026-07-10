using System.Text.Json;
using System.Text.Json.Serialization;
using AgentPlatform.Core.Entities;
using AgentPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentPlatform.Infrastructure.Services;

/// <summary>
/// 内存数据库快照 DTO：保存所有实体类型的数据
/// </summary>
public class DatabaseSnapshot
{
    public DateTime SavedAt { get; set; }
    public List<Agent>? Agents { get; set; }
    public List<AgentConfiguration>? AgentConfigurations { get; set; }
    public List<AgentSkill>? AgentSkills { get; set; }
    public List<AgentMcpEndpoint>? AgentMcpEndpoints { get; set; }
    public List<ModelProvider>? ModelProviders { get; set; }
    public List<ModelEndpoint>? ModelEndpoints { get; set; }
    public List<Skill>? Skills { get; set; }
    public List<McpEndpoint>? McpEndpoints { get; set; }
    public List<McpTool>? McpTools { get; set; }
    public List<Session>? Sessions { get; set; }
    public List<Conversation>? Conversations { get; set; }
    public List<UsageRecord>? UsageRecords { get; set; }
    public List<ApiKey>? ApiKeys { get; set; }
    public List<AgentVersion>? AgentVersions { get; set; }
    public List<AuditLog>? AuditLogs { get; set; }
    public List<User>? Users { get; set; }
}

/// <summary>
/// 数据库快照服务：每3秒将 InMemory 数据持久化到本地 JSON 文件，
/// 下次启动时自动恢复数据。
/// </summary>
public class DatabaseSnapshotService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseSnapshotService> _logger;
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DatabaseSnapshotService(IServiceScopeFactory scopeFactory, ILogger<DatabaseSnapshotService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "db-snapshot.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("数据库快照服务已启动，每3秒保存一次到: {Path}", _filePath);

        // 延迟3秒开始首次保存，确保应用完全就绪
        await Task.Delay(3000, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SaveSnapshotAsync(stoppingToken);
        }
    }

    /// <summary>
    /// 从本地快照文件恢复数据库。返回是否成功加载。
    /// </summary>
    public async Task<bool> TryLoadSnapshotAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("未找到快照文件 ({Path})，跳过恢复", _filePath);
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();

            var json = await File.ReadAllTextAsync(_filePath, ct);
            var snapshot = JsonSerializer.Deserialize<DatabaseSnapshot>(json, JsonOptions);
            if (snapshot == null) return false;

            int total = 0;

            void AddIfAny<T>(List<T>? items, DbSet<T> dbSet) where T : class
            {
                if (items != null && items.Count > 0)
                {
                    dbSet.AddRange(items);
                    total += items.Count;
                }
            }

            AddIfAny(snapshot.Agents, db.Agents);
            AddIfAny(snapshot.AgentConfigurations, db.AgentConfigurations);
            AddIfAny(snapshot.AgentSkills, db.AgentSkills);
            AddIfAny(snapshot.AgentMcpEndpoints, db.AgentMcpEndpoints);
            AddIfAny(snapshot.ModelProviders, db.ModelProviders);
            AddIfAny(snapshot.ModelEndpoints, db.ModelEndpoints);
            AddIfAny(snapshot.Skills, db.Skills);
            AddIfAny(snapshot.McpEndpoints, db.McpEndpoints);
            AddIfAny(snapshot.McpTools, db.McpTools);
            AddIfAny(snapshot.Sessions, db.Sessions);
            AddIfAny(snapshot.Conversations, db.Conversations);
            AddIfAny(snapshot.UsageRecords, db.UsageRecords);
            AddIfAny(snapshot.ApiKeys, db.ApiKeys);
            AddIfAny(snapshot.AgentVersions, db.AgentVersions);
            AddIfAny(snapshot.AuditLogs, db.AuditLogs);
            AddIfAny(snapshot.Users, db.Users);

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("已从快照恢复数据库，共 {TotalCount} 条记录", total);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载数据库快照失败");
            return false;
        }
    }

    private async Task SaveSnapshotAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();

            var snapshot = new DatabaseSnapshot
            {
                SavedAt = DateTime.UtcNow,
                Agents = await db.Agents.AsNoTracking().ToListAsync(ct),
                AgentConfigurations = await db.AgentConfigurations.AsNoTracking().ToListAsync(ct),
                AgentSkills = await db.AgentSkills.AsNoTracking().ToListAsync(ct),
                AgentMcpEndpoints = await db.AgentMcpEndpoints.AsNoTracking().ToListAsync(ct),
                ModelProviders = await db.ModelProviders.AsNoTracking().ToListAsync(ct),
                ModelEndpoints = await db.ModelEndpoints.AsNoTracking().ToListAsync(ct),
                Skills = await db.Skills.AsNoTracking().ToListAsync(ct),
                McpEndpoints = await db.McpEndpoints.AsNoTracking().ToListAsync(ct),
                McpTools = await db.McpTools.AsNoTracking().ToListAsync(ct),
                Sessions = await db.Sessions.AsNoTracking().ToListAsync(ct),
                Conversations = await db.Conversations.AsNoTracking().ToListAsync(ct),
                UsageRecords = await db.UsageRecords.AsNoTracking().ToListAsync(ct),
                ApiKeys = await db.ApiKeys.AsNoTracking().ToListAsync(ct),
                AgentVersions = await db.AgentVersions.AsNoTracking().ToListAsync(ct),
                AuditLogs = await db.AuditLogs.AsNoTracking().ToListAsync(ct),
                Users = await db.Users.AsNoTracking().ToListAsync(ct)
            };

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var dir = Path.GetDirectoryName(_filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存数据库快照失败");
        }
    }
}
