namespace AgentPlatform.Core.Interfaces;

public interface ISkillExecutor
{
    string Name { get; }
    string Description { get; }
    Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct);
}

public class SkillContext
{
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public string? UserId { get; set; }
}

public class SkillResult
{
    public bool Success { get; set; }
    public string? Data { get; set; }
    public string? Error { get; set; }
}
