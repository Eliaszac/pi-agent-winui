using PiAgentGui.Models.Docker;

namespace PiAgentGui.Services.Docker;

public interface IDockerClient
{
    Task<bool> IsInstalledAsync(DockerSource source, CancellationToken cancellationToken);
    Task<IReadOnlyList<DockerContainer>> ListAsync(DockerSource source, CancellationToken cancellationToken);
    Task SetRunningAsync(DockerSource source, string containerId, bool running, CancellationToken cancellationToken);
}
