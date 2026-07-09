namespace AgentPlatform.Core.Entities;

public class AgentSkill
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid SkillId { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;

    public Agent? Agent { get; set; }
    public Skill? Skill { get; set; }
}
