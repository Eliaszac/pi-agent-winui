namespace PiAgentGui.Models.Projects;

public sealed record ProjectScriptSettings
{
    public IReadOnlyList<ProjectScript> Scripts { get; init; } = [];
    public Guid? SelectedId { get; init; }
}
