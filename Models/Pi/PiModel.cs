namespace PiAgentGui.Models.Pi;

public sealed record PiModel(string Provider, string Id, string Name)
{
    public string DisplayName => $"{Name} · {Provider}";
}
