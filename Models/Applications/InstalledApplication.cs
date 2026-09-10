namespace PiAgentGui.Models.Applications;

public sealed record InstalledApplication(string Id, string Name, string ExecutablePath, string Logo, ApplicationKind Kind = ApplicationKind.Editor);
