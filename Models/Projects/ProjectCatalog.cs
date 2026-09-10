using System.Text.Json.Serialization;

namespace PiAgentGui.Models.Projects;

/// <summary>The versioned on-disk project catalog.</summary>
internal sealed record ProjectCatalog
{
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonRequired]
    public List<Project> Projects { get; init; } = [];
}
