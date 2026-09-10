namespace PiAgentGui.Models.Pi;

/// <summary>The working directory and dedicated Pi-owned session for one conversation.</summary>
public sealed record PiLaunchRequest(string WorkingDirectory, string SessionFile, string? SessionName = null, bool ManageProviders = false);
