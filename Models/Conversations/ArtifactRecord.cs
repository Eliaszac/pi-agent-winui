namespace PiAgentGui.Models.Conversations;

public sealed record ArtifactRecord(Guid Id, string Name, string MimeType, long Size, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, Guid Version, string Origin, string? SourceKey = null, bool Deleted = false, bool Shared = true);
