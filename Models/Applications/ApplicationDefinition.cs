namespace PiAgentGui.Models.Applications;

public sealed record ApplicationDefinition(string Id, string Name, string Executable, string Logo, string? Folder = null,
    ApplicationKind Kind = ApplicationKind.Editor);
