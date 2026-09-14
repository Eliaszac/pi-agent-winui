using PiAgentGui.Configuration;

namespace PiAgentGui.Utilities;

/// <summary>Maps immutable project/conversation identifiers to a stable Pi session file.</summary>
public sealed class PiSessionPaths(ProjectStorageOptions storage)
{
    private readonly string root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(storage.CatalogPath))!, "sessions");
    public string CleanupDirectory => Path.Combine(Path.GetDirectoryName(root)!, "deleted-conversation-data");
    public string UsageArchiveFile => Path.Combine(Path.GetDirectoryName(root)!, "usage-history.json");

    public string GetSessionFile(Guid projectId, Guid conversationId)
    {
        if (projectId == Guid.Empty || conversationId == Guid.Empty) throw new ArgumentException("Session identifiers cannot be empty.");
        return Path.Combine(root, projectId.ToString("N"), conversationId.ToString("N") + ".jsonl");
    }
}
