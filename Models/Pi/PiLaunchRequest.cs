namespace PiAgentGui.Models.Pi;

/// <summary>The working directory and dedicated Pi-owned session for one conversation.</summary>
public sealed record PiLaunchRequest(string WorkingDirectory, string SessionFile, string? SessionName = null, bool ManageProviders = false,
    bool ResearchWorker = false, string? Provider = null, string? Model = null, string? Effort = null, string? ResearchPreferencePath = null,
    Models.Projects.ExecutionTarget? Target = null, string? ManageMcpServer = null);
