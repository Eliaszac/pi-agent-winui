namespace PiAgentGui.Models.Docker;

public sealed record DockerContainer(Guid SourceId, string Id, string Name, string Image, string State, string Status)
{
    public string Key => $"{SourceId:N}/{Id}";
    public bool CanStart => State is "created" or "exited";
    public bool CanStop => State is "running" or "restarting";
}
