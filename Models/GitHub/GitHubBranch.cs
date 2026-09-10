namespace PiAgentGui.Models.GitHub;

public sealed record GitHubBranch(string Owner, string Repository, string Branch)
{
    public string FullName => $"{Owner}/{Repository}";
}
