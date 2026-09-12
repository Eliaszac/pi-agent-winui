namespace PiAgentGui.Models.Conversations;

public sealed record CheckpointManifest(string Id, string Response, string State, bool Overlap, int Omitted,
    IReadOnlyList<FileChange> Files, IReadOnlyList<string> Applied);
