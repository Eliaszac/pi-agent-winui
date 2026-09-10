using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Services.GitHub;

public interface IGitBranchReader
{
    Task<bool> IsRepositoryAsync(string directory, CancellationToken cancellationToken);
    Task<GitHubBranch?> ReadAsync(string directory, CancellationToken cancellationToken);
}
