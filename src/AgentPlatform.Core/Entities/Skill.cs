namespace AgentPlatform.Core.Entities;

public class Skill
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SkillType Type { get; set; } = SkillType.Tool;
    public string Implementation { get; set; } = string.Empty;
    public string InputSchema { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AgentSkill> AgentSkills { get; set; } = new();
}

public enum SkillType
{
    Tool,
    Api,
    Script,
    Composite
}
