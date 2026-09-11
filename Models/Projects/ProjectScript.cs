namespace PiAgentGui.Models.Projects;

/// <summary>A user-authored PowerShell command belonging to one project.</summary>
public sealed record ProjectScript(Guid Id, string Name, string Command, string WorkingDirectory = "");
