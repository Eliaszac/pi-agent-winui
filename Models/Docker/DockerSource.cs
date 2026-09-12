using PiAgentGui.Models.Projects;

namespace PiAgentGui.Models.Docker;

public sealed record DockerSource(Guid Id, ExecutionTarget Target)
{
    public string Name => Target.IsLocal ? "Windows" : $"{(Target.Kind == "wsl" ? "WSL" : "SSH")} · {Target.Host}";
}
