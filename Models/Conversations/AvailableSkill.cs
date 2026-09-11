namespace PiAgentGui.Models.Conversations;

/// <summary>A skill discovered by the selected Pi runtime, not a claim that its instructions were read.</summary>
public sealed record AvailableSkill(string Name, string Description, string Scope, string Path)
{
    public string Command => "/skill:" + Name;
}
