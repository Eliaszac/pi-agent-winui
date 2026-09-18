namespace PiAgentGui.Models.Projects;

/// <summary>A reviewed script snapshot, bound to its loaded target and conversation.</summary>
public sealed record ProjectScriptRunRequest(ProjectScript Script, string Directory, string TargetLabel,
    int Revision, Guid? ConversationId);
