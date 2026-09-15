using System.Text.Json;

namespace PiAgentGui.Utilities;

public static class ArtifactResult
{
    public static Guid? Read(string tool, bool success, JsonElement details) =>
        tool == "artifact_save" && success && Guid.TryParse(PiJson.Text(details, "artifactId"), out var id) && id != Guid.Empty ? id : null;
}
