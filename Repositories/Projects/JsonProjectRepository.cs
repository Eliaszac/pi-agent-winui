using System.Text.Json;
using System.Text.Json.Serialization;
using PiAgentGui.Configuration;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.Validators;

namespace PiAgentGui.Repositories.Projects;

/// <summary>Stores a versioned JSON catalog using exclusive access and atomic replacement.</summary>
public sealed class JsonProjectRepository : IProjectRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly string catalogPath;

    /// <summary>Creates a repository without reading or writing the catalog.</summary>
    /// <param name="options">The application-owned storage location.</param>
    public JsonProjectRepository(ProjectStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        catalogPath = ProjectPath.Normalize(options.CatalogPath);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var catalogLock = AcquireLock();
        var catalog = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return catalog.Projects.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        ProjectValidator.Validate(project);
        var savedProject = project with
        {
            Name = project.Name.Trim(),
            Path = ProjectPath.Normalize(project.Path),
            Metadata = project.Metadata.Clone()
        };

        cancellationToken.ThrowIfCancellationRequested();
        using var catalogLock = AcquireLock();
        var catalog = await ReadAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.Projects.Any(existing => existing.Id == savedProject.Id ||
            StringComparer.OrdinalIgnoreCase.Equals(ProjectPath.Normalize(existing.Path), savedProject.Path)))
            throw new InvalidOperationException("This project identifier or directory is already registered.");

        catalog.Projects.Add(savedProject);
        await WriteAsync(catalog, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateMetadataAsync(Guid projectId, JsonElement metadata, CancellationToken cancellationToken = default)
    {
        ProjectValidator.ValidateMetadata(metadata);
        var savedMetadata = metadata.Clone();
        cancellationToken.ThrowIfCancellationRequested();
        using var catalogLock = AcquireLock();
        var catalog = await ReadAsync(cancellationToken).ConfigureAwait(false);
        var index = catalog.Projects.FindIndex(project => project.Id == projectId);
        if (index < 0)
            throw new KeyNotFoundException("The project no longer exists.");

        catalog.Projects[index] = catalog.Projects[index] with { Metadata = savedMetadata };
        await WriteAsync(catalog, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ConversationDraft> AddConversationAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var catalogLock = AcquireLock();
        var catalog = await ReadAsync(cancellationToken).ConfigureAwait(false);
        var index = catalog.Projects.FindIndex(project => project.Id == projectId);
        if (index < 0)
            throw new KeyNotFoundException("The project no longer exists.");

        var project = catalog.Projects[index];
        var conversation = new ConversationDraft
        {
            Id = Guid.NewGuid(),
            Title = $"Conversation {project.Conversations.Count + 1}",
            IsTitleManual = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        catalog.Projects[index] = project with { Conversations = [.. project.Conversations, conversation] };
        await WriteAsync(catalog, cancellationToken).ConfigureAwait(false);
        return conversation;
    }

    private FileStream AcquireLock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        // Keep the lock file in place: deleting it could race with another process opening it.
        return new FileStream(catalogPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public Task AddConversationCopyAsync(Guid projectId, Guid sourceId, ConversationDraft conversation, CancellationToken cancellationToken = default) =>
        ChangeProjectAsync(projectId, project =>
        {
            if (!project.Conversations.Any(item => item.Id == sourceId)) throw new KeyNotFoundException("The original conversation no longer exists.");
            var updated = project with { Conversations = [.. project.Conversations, conversation] };
            ProjectValidator.Validate(updated);
            return updated;
        }, cancellationToken);

    public Task RenameProjectAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ChangeProjectAsync(projectId, project => project with { Name = name.Trim() }, cancellationToken);
    }

    public Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        ChangeProjectAsync(projectId, _ => null, cancellationToken);

    public Task RenameConversationAsync(Guid projectId, Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return ChangeProjectAsync(projectId, project => ChangeConversation(project, conversationId,
            conversation => conversation with { Title = title.Trim(), IsTitleManual = true }), cancellationToken);
    }

    public async Task<bool> SetGeneratedTitleAsync(Guid projectId, Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var applied = false;
        await ChangeProjectAsync(projectId, project => ChangeConversation(project, conversationId, conversation =>
        {
            if (conversation.IsTitleManual) return conversation;
            applied = true;
            return conversation with { Title = title.Trim() };
        }), cancellationToken).ConfigureAwait(false);
        return applied;
    }

    public Task SetConversationSettledAsync(Guid projectId, Guid conversationId, bool settled, CancellationToken cancellationToken = default) =>
        ChangeProjectAsync(projectId, project => ChangeConversation(project, conversationId,
            conversation => conversation with { IsSettled = settled }), cancellationToken);

    public Task DeleteConversationAsync(Guid projectId, Guid conversationId, CancellationToken cancellationToken = default) =>
        ChangeProjectAsync(projectId, project => ChangeConversation(project, conversationId, _ => null), cancellationToken);

    public Task TouchConversationAsync(Guid projectId, Guid conversationId, DateTimeOffset usedAt, CancellationToken cancellationToken = default) =>
        ChangeProjectAsync(projectId, project => ChangeConversation(project, conversationId,
            conversation => conversation with { LastUsedAt = conversation.LastUsedAt > usedAt ? conversation.LastUsedAt : usedAt }), cancellationToken);

    private static Project ChangeConversation(Project project, Guid conversationId, Func<ConversationDraft, ConversationDraft?> change)
    {
        var conversations = project.Conversations.ToList();
        var index = conversations.FindIndex(conversation => conversation.Id == conversationId);
        if (index < 0) throw new KeyNotFoundException("The conversation no longer exists.");
        var updated = change(conversations[index]);
        if (updated is null) conversations.RemoveAt(index);
        else conversations[index] = updated;
        return project with { Conversations = conversations };
    }

    private async Task ChangeProjectAsync(Guid projectId, Func<Project, Project?> change, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var catalogLock = AcquireLock();
        var catalog = await ReadAsync(cancellationToken).ConfigureAwait(false);
        var index = catalog.Projects.FindIndex(project => project.Id == projectId);
        if (index < 0) throw new KeyNotFoundException("The project no longer exists.");
        var updated = change(catalog.Projects[index]);
        if (updated is null) catalog.Projects.RemoveAt(index);
        else { ProjectValidator.Validate(updated); catalog.Projects[index] = updated; }
        await WriteAsync(catalog, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProjectCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(catalogPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.Asynchronous);
            var catalog = await JsonSerializer.DeserializeAsync<ProjectCatalog>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("The project catalog is empty or invalid.");

            if (catalog.SchemaVersion != 1)
                throw new InvalidDataException("The project catalog version is not supported.");
            if (catalog.Projects is null)
                throw new InvalidDataException("The project list is missing.");

            var identifiers = new HashSet<Guid>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in catalog.Projects)
            {
                ProjectValidator.Validate(project);
                if (!identifiers.Add(project.Id) || !paths.Add(ProjectPath.Normalize(project.Path)))
                    throw new InvalidDataException("The project catalog contains duplicate projects.");
            }

            return catalog;
        }
        catch (FileNotFoundException)
        {
            return new ProjectCatalog { SchemaVersion = 1 };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException)
        {
            throw new InvalidDataException("The project catalog is invalid; the original file has been preserved.", exception);
        }
    }

    private async Task WriteAsync(ProjectCatalog catalog, CancellationToken cancellationToken)
    {
        var temporaryPath = catalogPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, catalog, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            // The temporary file lives beside the catalog so the rename stays on the same volume.
            File.Move(temporaryPath, catalogPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
