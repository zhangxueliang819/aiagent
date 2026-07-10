using AgentPlatform.Application.Services;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.Infrastructure.Data;
using AgentPlatform.Infrastructure.Repositories;
using AgentPlatform.Infrastructure.Services;
using AgentPlatform.AgentEngine.Memory;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.AgentEngine.Runtime;
using AgentPlatform.AgentEngine.Services;
using AgentPlatform.ModelProviders.Mcp;
using AgentPlatform.ModelProviders.Simulated;
using AgentPlatform.Web.Controllers;
using AgentPlatform.Web.Hubs;
using AgentPlatform.Web.Middleware;
using AgentPlatform.Core.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

    // EF Core InMemory
    builder.Services.AddDbContext<AgentPlatformDbContext>(options =>
        options.UseInMemoryDatabase("AgentPlatform"));

    // Repositories
    builder.Services.AddScoped<IAgentRepository, AgentRepository>();
    builder.Services.AddScoped<IModelProviderRepository, ModelProviderRepository>();
    builder.Services.AddScoped<ISkillRepository, SkillRepository>();
    builder.Services.AddScoped<ISessionRepository, SessionRepository>();
    builder.Services.AddScoped<IUsageRepository, UsageRepository>();
    builder.Services.AddScoped<IMcpEndpointRepository, McpEndpointRepository>();
    builder.Services.AddScoped<IMcpToolRepository, McpToolRepository>();
    builder.Services.AddScoped<IAgentMcpEndpointRepository, AgentMcpEndpointRepository>();
    builder.Services.AddScoped<IAgentSkillRepository, AgentSkillRepository>();
    builder.Services.AddScoped<IAgentVersionRepository, AgentVersionRepository>();
    builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Services
    builder.Services.AddScoped<AgentService>();
    builder.Services.AddScoped<ModelProviderService>();
    builder.Services.AddScoped<ModelRouter>();
    builder.Services.AddScoped<SkillService>();
    builder.Services.AddScoped<SessionService>();
    builder.Services.AddScoped<UsageService>();
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddScoped<UserService>();

    // Rate Limiting & Metrics
    builder.Services.AddSingleton<RateLimiter>();
    builder.Services.AddSingleton<ModelMetricsCollector>();

    // Agent Engine - Skill Executors
    builder.Services.AddSingleton<ISkillExecutor, ToolSkillExecutor>();
    builder.Services.AddSingleton<ISkillExecutor, ApiSkillExecutor>();
    builder.Services.AddSingleton<ISkillExecutor, ScriptSkillExecutor>();
    builder.Services.AddSingleton<ISkillExecutor, CompositeSkillExecutor>();

    // Agent Engine - Short-Term Memory
    builder.Services.AddScoped<IShortTermMemoryStore, ShortTermMemoryStore>();

    // Agent Engine - Runtime
    builder.Services.AddSingleton<SkillDispatcher>();
    builder.Services.AddSingleton<FunctionCallHandler>();
    builder.Services.AddSingleton<AgentRuntime>();

    // Background Services
    builder.Services.AddHostedService<SessionCleanupService>();
    builder.Services.AddSingleton<DatabaseSnapshotService>();
    builder.Services.AddHostedService<DatabaseSnapshotService>(sp => sp.GetRequiredService<DatabaseSnapshotService>());

    // MCP Client
    builder.Services.AddHttpClient<McpClient>();

    // Simulated LLM (dev fallback when Agent has no ModelEndpoint configured)
    builder.Services.AddSingleton<SimulatedModelProvider>();
    builder.Services.AddSingleton<IModelProvider>(sp => sp.GetRequiredService<SimulatedModelProvider>());

    // Controllers
    builder.Services.AddControllers();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Agent Platform API", Version = "v1" });
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        });
    });

    // JWT Authentication
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"] ?? "AgentPlatform_SuperSecret_Key_2024_Must_Be_At_Least_32_Chars!";
    var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
        };
        // SignalR 支持从 QueryString 读取 Token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
    builder.Services.AddAuthorization();

    // SignalR
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<ChatEventBroadcaster>();

    var app = builder.Build();

    // Middleware pipeline
    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agent Platform API v1"));
    app.UseCors();

    // Authentication & Authorization
    app.UseAuthentication();

    // API Key 认证中间件（在 JWT 之后、Authorization 之前检查）
    app.UseMiddleware<ApiKeyAuthMiddleware>();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");

    // Seed InMemory database
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
        db.Database.EnsureCreated();
    }

    // 从快照恢复数据（如果存在）
    var snapshotSvc = app.Services.GetRequiredService<DatabaseSnapshotService>();
    var restored = await snapshotSvc.TryLoadSnapshotAsync();
    if (!restored)
    {
        // 首次启动，手动创建种子数据（替代 HasData）
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
            if (!await db.ModelProviders.AnyAsync())
            {
                db.ModelProviders.Add(new ModelProvider
                {
                    Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    Name = "OpenAI Default",
                    ProviderType = "OpenAI",
                    ApiBaseUrl = "https://api.openai.com/v1",
                    CreatedAt = DateTime.UtcNow
                });
                db.ModelEndpoints.Add(new ModelEndpoint
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                    ModelProviderId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    ModelId = "gpt-4o",
                    ModelName = "GPT-4o",
                    MaxTokens = 128000,
                    IsEnabled = true
                });
                await db.SaveChangesAsync();
                Log.Information("默认种子数据已创建");
            }
        }
    }

    // 输出运行地址
    var urls = app.Urls.Any()
        ? string.Join(", ", app.Urls)
        : "http://localhost:5000";
    Log.Information("Agent Platform API 已启动，监听地址: {Urls}", urls);
    Log.Information("Swagger UI: {Url}/swagger", app.Urls.FirstOrDefault() ?? "http://localhost:5000");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
