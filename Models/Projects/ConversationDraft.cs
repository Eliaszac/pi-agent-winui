namespace PiAgentGui.Models.Projects;

/// <summary>A local conversation identity; its Pi session path is derived from this ID and its project ID.</summary>
public sealed record ConversationDraft
{
    /// <summary>Gets the stable local conversation identifier.</summary>
    public required Guid Id { get; init; }
    public Guid? TargetId { get; init; }

    /// <summary>Gets the title displayed in the project list.</summary>
    public required string Title { get; init; }
    /// <summary>Legacy titles are protected; only newly created drafts opt into automatic naming.</summary>
    public bool IsTitleManual { get; init; } = true;

    /// <summary>Gets the UTC creation time.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>Gets the most recent explicit opening time; older catalogs fall back to creation time.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }
    /// <summary>Gets whether the user has moved this conversation into the settled group.</summary>
    public bool IsSettled { get; init; }
}
