using AgentPlatform.Application.Services;
using AgentPlatform.Core.Interfaces;
using AgentPlatform.Infrastructure.Data;
using AgentPlatform.Infrastructure.Repositories;
using AgentPlatform.AgentEngine.Memory;
using AgentPlatform.AgentEngine.Skills;
using AgentPlatform.AgentEngine.Runtime;
using AgentPlatform.ModelProviders.Mcp;
using AgentPlatform.ModelProviders.Simulated;
using AgentPlatform.Web.Controllers;
using AgentPlatform.Web.Hubs;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

    // Services
    builder.Services.AddScoped<AgentService>();
    builder.Services.AddScoped<ModelProviderService>();
    builder.Services.AddScoped<ModelRouter>();
    builder.Services.AddScoped<SkillService>();
    builder.Services.AddScoped<SessionService>();
    builder.Services.AddScoped<UsageService>();

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

    // SignalR
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<ChatEventBroadcaster>();

    var app = builder.Build();

    // Middleware pipeline
    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agent Platform API v1"));
    app.UseCors();
    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");

    // Seed InMemory database
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
        db.Database.EnsureCreated();
    }

    Log.Information("Agent Platform API starting...");
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
