namespace PiAgentGui.Models.SourceControl;

public sealed record GitSnapshot(string Root, string HeadRef, bool HasHead, IReadOnlyList<GitChange> Changes,
    IReadOnlyList<GitBranch> Branches, IReadOnlyList<string> Remotes, bool Conflicted)
{
    public string BranchName => HeadRef.StartsWith("refs/heads/", StringComparison.Ordinal) ? HeadRef[11..] : "Detached HEAD";
    public bool Detached => !HeadRef.StartsWith("refs/heads/", StringComparison.Ordinal);
}
