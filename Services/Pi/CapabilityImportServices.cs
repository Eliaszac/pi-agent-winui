namespace PiAgentGui.Services.Pi;

/// <summary>Dependencies composed by App for the native capability import dialogs.</summary>
public sealed record CapabilityImportServices(GlobalSkillRegistration Skills, PiPackageInstaller Packages, McpConfigImporter Mcp);
