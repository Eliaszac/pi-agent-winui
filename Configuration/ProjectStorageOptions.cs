namespace PiAgentGui.Configuration;

/// <summary>Configures the application-owned project catalog location.</summary>
public sealed record ProjectStorageOptions
{
    /// <summary>Gets the catalog path. Tests may supply an isolated location.</summary>
    public string CatalogPath { get; init; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PiAgentGui", "projects.json");
}
