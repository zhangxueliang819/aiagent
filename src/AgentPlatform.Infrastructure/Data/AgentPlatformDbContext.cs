using AgentPlatform.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentPlatform.Infrastructure.Data;

public class AgentPlatformDbContext : DbContext
{
    public AgentPlatformDbContext(DbContextOptions<AgentPlatformDbContext> options) : base(options) { }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentConfiguration> AgentConfigurations => Set<AgentConfiguration>();
    public DbSet<AgentSkill> AgentSkills => Set<AgentSkill>();
    public DbSet<AgentMcpEndpoint> AgentMcpEndpoints => Set<AgentMcpEndpoint>();
    public DbSet<ModelProvider> ModelProviders => Set<ModelProvider>();
    public DbSet<ModelEndpoint> ModelEndpoints => Set<ModelEndpoint>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<McpEndpoint> McpEndpoints => Set<McpEndpoint>();
    public DbSet<McpTool> McpTools => Set<McpTool>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<AgentVersion> AgentVersions => Set<AgentVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name);
            e.HasOne(x => x.ModelEndpoint).WithMany().HasForeignKey(x => x.ModelEndpointId).IsRequired(false);
            e.HasMany(x => x.Configurations).WithOne(x => x.Agent).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Skills).WithOne(x => x.Agent).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.McpEndpoints).WithOne(x => x.Agent).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentConfiguration>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<AgentSkill>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Skill).WithMany(x => x.AgentSkills).HasForeignKey(x => x.SkillId);
        });

        modelBuilder.Entity<ModelProvider>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Endpoints).WithOne(x => x.ModelProvider).HasForeignKey(x => x.ModelProviderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModelEndpoint>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Skill>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<McpEndpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Tools).WithOne(x => x.McpEndpoint).HasForeignKey(x => x.McpEndpointId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.AgentMcpEndpoints).WithOne(x => x.McpEndpoint).HasForeignKey(x => x.McpEndpointId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentMcpEndpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Agent).WithMany(x => x.McpEndpoints).HasForeignKey(x => x.AgentId);
            e.HasOne(x => x.McpEndpoint).WithMany(x => x.AgentMcpEndpoints).HasForeignKey(x => x.McpEndpointId);
        });

        modelBuilder.Entity<McpTool>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Session>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasMany(x => x.Conversations).WithOne(x => x.Session).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<UsageRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        modelBuilder.Entity<AgentVersion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AgentId, x.VersionNumber }).IsUnique();
            e.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.EntityType);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
        });

        // Seed default data
        modelBuilder.Entity<ModelProvider>().HasData(new ModelProvider
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "OpenAI Default",
            ProviderType = "OpenAI",
            ApiBaseUrl = "https://api.openai.com/v1",
            EncryptedApiKey = "",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<ModelEndpoint>().HasData(new ModelEndpoint
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            ModelProviderId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ModelId = "gpt-4o",
            ModelName = "GPT-4o",
            MaxTokens = 128000,
            IsEnabled = true
        });
    }
}
