namespace PiAgentGui.ViewModels.Extensions;

public sealed record ExtensionDefinition(string Name, string Author, string Version, string Description,
    string Details, string InstallCommand, string Configuration, string ConfigurationJson,
    Uri Documentation, Uri Source, Func<(string Status, bool NeedsSetup)> CheckInstallation, bool RecommendationOnly = false, bool Bundled = false, bool Essential = false,
    Func<bool>? ReadEnabled = null, Func<bool, Task>? WriteEnabled = null,
    string? ToggleHint = null, Docker.DockerPanelViewModel? Docker = null);
