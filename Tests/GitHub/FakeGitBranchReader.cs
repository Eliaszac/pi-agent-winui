using PiAgentGui.Models.GitHub;
using PiAgentGui.Services.GitHub;

namespace PiAgentGui.Tests.GitHub;

public sealed class FakeGitBranchReader : IGitBranchReader
{
    public GitHubBranch? Branch { get; set; } = new("owner", "repo", "feature");
    public Task<GitHubBranch?> ReadAsync(string directory, CancellationToken cancellationToken) => Task.FromResult(Branch);
}
