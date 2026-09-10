using PiAgentGui.Models.GitHub;
using PiAgentGui.Services.GitHub;

namespace PiAgentGui.Tests.GitHub;

public sealed class FakeGitHubCredentials : IGitHubCredentialStore
{
    public GitHubToken? Token { get; set; }
    public GitHubToken? Read() => Token;
    public void Save(GitHubToken token) => Token = token;
    public void Clear() => Token = null;
}
