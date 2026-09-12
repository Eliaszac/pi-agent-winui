using System.Text.Json;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Projects;

/// <summary>Registers existing working directories as named projects.</summary>
public sealed class ProjectService
{
    public TargetSetupService TargetSetup { get; } = new(new TargetCommandRunner());
    public async Task<Project> CreateWithTargetAsync(string name, ExecutionTarget target, string? repositoryUrl = null, CancellationToken cancellationToken = default, string? sshSecret = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        target = await TargetSetup.PrepareAsync(target, repositoryUrl, cancellationToken, sshSecret).ConfigureAwait(false);
        var project = new Project { Id = target.Id, Name = name.Trim(), Path = target.Path, Targets = [target], DefaultTargetId = target.Id };
        await repository.AddAsync(project, cancellationToken).ConfigureAwait(false);
        return project;
    }
    private readonly IProjectRepository repository;

    /// <summary>Creates the service with its persistence boundary.</summary>
    /// <param name="repository">The project repository.</param>
    public ProjectService(IProjectRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        this.repository = repository;
    }

    /// <summary>Validates and saves a project without modifying its working directory.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="path">An existing absolute working directory.</param>
    /// <param name="metadata">Optional JSON object data; defaults to an empty object.</param>
    /// <param name="cancellationToken">Cancels before persistence commits.</param>
    /// <returns>The saved project.</returns>
    public async Task<Project> CreateAsync(string name, string path, JsonElement? metadata = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedPath = ProjectPath.Normalize(path);
        if (!Directory.Exists(normalizedPath))
            throw new DirectoryNotFoundException("Choose an existing, accessible project directory.");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Path = normalizedPath,
            Metadata = metadata?.Clone() ?? JsonSerializer.SerializeToElement(new { })
        };
        await repository.AddAsync(project, cancellationToken).ConfigureAwait(false);
        return project;
    }
}
