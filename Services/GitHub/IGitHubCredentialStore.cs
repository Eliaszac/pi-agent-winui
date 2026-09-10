using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Services.GitHub;

public interface IGitHubCredentialStore
{
    GitHubToken? Read();
    void Save(GitHubToken token);
    void Clear();
}
