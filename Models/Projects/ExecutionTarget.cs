using System.Text.Json.Serialization;

namespace PiAgentGui.Models.Projects;

/// <summary>A named workspace location. Credentials remain in the operating system's SSH configuration.</summary>
public sealed record ExecutionTarget
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string Kind { get; init; } = "local";
    public required string Path { get; init; }
    public string Host { get; init; } = "";
    public string SshAuthentication { get; init; } = "key";
    public string SshKeyPath { get; init; } = "";
    public bool HasSshSecret { get; init; }
    [JsonIgnore] public bool IsLocal => Kind == "local";
    [JsonIgnore] public string Label => Kind switch { "wsl" => $"WSL · {Name}", "ssh" => $"SSH · {Name}", _ => $"Local · {Name}" };
    [JsonIgnore] public string Description => $"{Label}\n{(IsLocal ? "This computer" : Host)}\n{Path}";
    [JsonIgnore] public string Glyph => Kind switch { "wsl" => "\uE756", "ssh" => "\uE968", _ => "\uE7F4" };
}
