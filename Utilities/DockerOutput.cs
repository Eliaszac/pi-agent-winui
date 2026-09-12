using System.Text.Json;
using PiAgentGui.Models.Docker;

namespace PiAgentGui.Utilities;

public static class DockerOutput
{
    public static bool IsContainerId(string id) => id.Length == 64 && id.All(char.IsAsciiHexDigit);

    public static IReadOnlyList<DockerContainer> Parse(Guid sourceId, string output)
    {
        var containers = new List<DockerContainer>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var json = JsonDocument.Parse(line);
            var row = json.RootElement;
            var id = row.GetProperty("ID").GetString() ?? "";
            if (!IsContainerId(id)) throw new IOException("Docker returned an invalid container ID.");
            containers.Add(new(sourceId, id, row.GetProperty("Names").GetString() ?? id,
                row.GetProperty("Image").GetString() ?? "", row.GetProperty("State").GetString() ?? "unknown",
                row.GetProperty("Status").GetString() ?? ""));
        }
        return containers.DistinctBy(container => container.Id).OrderBy(container => container.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool Visible(DockerContainer container, Guid? projectId, IReadOnlyDictionary<string, Guid[]> links) =>
        !links.TryGetValue(container.Id, out var projects) || projects.Length == 0 || projectId is { } id && projects.Contains(id);
}
