namespace PiAgentGui.Models.Pi;

/// <summary>The executable and optional JavaScript entry point used to start Pi.</summary>
public sealed record PiInstallation(string ExecutablePath, string? CliPath = null);
