using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Services.GitHub;

public interface IGitBranchReader
{
    Task<GitHubBranch?> ReadAsync(string directory, CancellationToken cancellationToken);
}
