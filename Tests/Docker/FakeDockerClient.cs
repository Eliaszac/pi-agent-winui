using PiAgentGui.Models.Docker;
using PiAgentGui.Services.Docker;

namespace PiAgentGui.Tests.Docker;

internal sealed class FakeDockerClient : IDockerClient
{
    public Dictionary<Guid, IReadOnlyList<DockerContainer>> Rows { get; } = [];
    public HashSet<Guid> Failures { get; } = [];
    public List<(Guid Source, string Container, bool Running)> Actions { get; } = [];
    public Func<DockerSource, CancellationToken, Task<IReadOnlyList<DockerContainer>>>? ListHandler { get; set; }
    public Func<Task>? ActionHandler { get; set; }
    public bool Installed { get; set; } = true;
    public int Lists { get; private set; }
    public Task<bool> IsInstalledAsync(DockerSource source, CancellationToken cancellationToken) => Task.FromResult(Installed);
    public Task<IReadOnlyList<DockerContainer>> ListAsync(DockerSource source, CancellationToken cancellationToken)
    {
        Lists++;
        if (ListHandler is { } handler) return handler(source, cancellationToken);
        if (Failures.Contains(source.Id)) throw new IOException("Cannot reach Docker engine");
        return Task.FromResult(Rows.GetValueOrDefault(source.Id) ?? (IReadOnlyList<DockerContainer>)[]);
    }
    public async Task SetRunningAsync(DockerSource source, string containerId, bool running, CancellationToken cancellationToken)
    {
        Actions.Add((source.Id, containerId, running));
        if (ActionHandler is { } handler) await handler();
    }
}
