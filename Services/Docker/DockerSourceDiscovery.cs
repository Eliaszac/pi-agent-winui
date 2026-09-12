using PiAgentGui.Models.Docker;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Docker;

public sealed class DockerSourceDiscovery(IProjectRepository projects, WslDistributionCache wsl, IDockerClient client)
{
    public string? Warning { get; private set; }
    public Task<IReadOnlyList<Project>> ProjectsAsync() => projects.GetAllAsync();

    public async Task<IReadOnlyList<DockerSource>> SshAsync() => (await projects.GetAllAsync())
        .SelectMany(ProjectTargets.All).Where(target => target.Kind == "ssh")
        .DistinctBy(target => (target.Host, target.SshAuthentication, target.SshKeyPath))
        .Select(target => new DockerSource(Guid.NewGuid(), target)).ToArray();

    public async Task<IReadOnlyList<DockerSource>> DetectAsync(CancellationToken cancellationToken)
    {
        var snapshot = await wsl.GetAsync(refresh: true);
        Warning = snapshot.Error;
        var targets = new[] { new ExecutionTarget { Id = Guid.NewGuid(), Name = "Windows", Path = Path.GetTempPath() } }
            .Concat(snapshot.Names.Where(name => !name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase))
                .Select(name => new ExecutionTarget { Id = Guid.NewGuid(), Name = name, Kind = "wsl", Host = name, Path = "/tmp" }));
        using var concurrency = new SemaphoreSlim(3);
        var found = await Task.WhenAll(targets.Select(async target =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                var source = new DockerSource(Guid.NewGuid(), target);
                return await client.IsInstalledAsync(source, cancellationToken) ? source : null;
            }
            finally { concurrency.Release(); }
        }));
        return found.OfType<DockerSource>().ToArray();
    }

    public static bool Same(DockerSource a, DockerSource b) => a.Target.Kind == b.Target.Kind &&
        a.Target.Host == b.Target.Host && a.Target.SshAuthentication == b.Target.SshAuthentication && a.Target.SshKeyPath == b.Target.SshKeyPath;
}
