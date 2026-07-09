namespace AgentPlatform.Core.Entities;

public class AgentConfiguration
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = "string";

    public Agent? Agent { get; set; }
}
