namespace PiAgentGui.Models.Projects;

/// <summary>A user-authored command belonging to one project and execution target.</summary>
public sealed record ProjectScript(Guid Id, string Name, string Command, string WorkingDirectory = "")
{
    public string Label => "Run script: " + Name;
}
