using System.Text.Json;

namespace PiAgentGui.Models.Projects;

/// <summary>A saved working directory and its application-owned metadata.</summary>
public sealed record Project
{
    /// <summary>Gets the stable identifier, independent of the name and path.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the user-facing display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the normalized absolute working directory.</summary>
    public required string Path { get; init; }

    /// <summary>Gets extensible JSON object data. Do not store credentials here.</summary>
    public JsonElement Metadata { get; init; } = JsonSerializer.SerializeToElement(new { });

    /// <summary>Gets local conversation entries in creation order.</summary>
    public IReadOnlyList<ConversationDraft> Conversations { get; init; } = [];
}
