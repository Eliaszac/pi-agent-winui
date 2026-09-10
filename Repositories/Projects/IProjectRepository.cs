using System.Text.Json;
using PiAgentGui.Models.Projects;

namespace PiAgentGui.Repositories.Projects;

/// <summary>Persists projects without exposing the storage format to callers.</summary>
public interface IProjectRepository
{
    Task RenameProjectAsync(Guid projectId, string name, CancellationToken cancellationToken = default);
    Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task RenameConversationAsync(Guid projectId, Guid conversationId, string title, CancellationToken cancellationToken = default);
    Task<bool> SetGeneratedTitleAsync(Guid projectId, Guid conversationId, string title, CancellationToken cancellationToken = default);
    Task SetConversationSettledAsync(Guid projectId, Guid conversationId, bool settled, CancellationToken cancellationToken = default);
    Task DeleteConversationAsync(Guid projectId, Guid conversationId, CancellationToken cancellationToken = default);
    /// <summary>Loads projects in their saved order, including unavailable directories.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The saved projects, or an empty list for a new catalog.</returns>
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a project, rejecting duplicate identifiers or normalized directory paths.</summary>
    /// <param name="project">The validated project to save.</param>
    /// <param name="cancellationToken">Cancels before the atomic commit.</param>
    Task AddAsync(Project project, CancellationToken cancellationToken = default);

    /// <summary>Replaces one project's metadata, preserving all other project fields.</summary>
    /// <param name="projectId">The saved project identifier.</param>
    /// <param name="metadata">The complete replacement JSON object.</param>
    /// <param name="cancellationToken">Cancels before the atomic commit.</param>
    Task UpdateMetadataAsync(Guid projectId, JsonElement metadata, CancellationToken cancellationToken = default);

    /// <summary>Creates a local conversation draft within a saved project.</summary>
    /// <param name="projectId">The owning project.</param>
    /// <param name="cancellationToken">Cancels before the atomic commit.</param>
    /// <returns>The persisted draft.</returns>
    Task<ConversationDraft> AddConversationAsync(Guid projectId, CancellationToken cancellationToken = default);
}
