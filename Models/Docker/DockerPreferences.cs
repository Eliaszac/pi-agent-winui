namespace PiAgentGui.Models.Docker;

public sealed record DockerPreferences(bool Enabled, IReadOnlyList<DockerSource> Sources, IReadOnlyDictionary<string, Guid[]> Links)
{
    public static DockerPreferences Empty => new(false, [], new Dictionary<string, Guid[]>());
}
