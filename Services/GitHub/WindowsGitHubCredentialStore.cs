using System.Text.Json;
using PiAgentGui.Models.GitHub;
using Windows.Security.Credentials;

namespace PiAgentGui.Services.GitHub;

/// <summary>Uses Windows Credential Locker; tokens never enter the project catalog or settings file.</summary>
public sealed class WindowsGitHubCredentialStore(string clientId) : IGitHubCredentialStore
{
    private string Resource => "PiAgentGui.GitHub." + clientId;
    public GitHubToken? Read()
    {
        PasswordCredential credential;
        try { credential = new PasswordVault().Retrieve(Resource, "user"); }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490)) { return null; }
        credential.RetrievePassword();
        try { return JsonSerializer.Deserialize<GitHubToken>(credential.Password); }
        catch (JsonException) { Clear(); return null; }
    }
    public void Save(GitHubToken token) => new PasswordVault().Add(new PasswordCredential(Resource, "user", JsonSerializer.Serialize(token)));
    public void Clear()
    {
        var vault = new PasswordVault();
        try { vault.Remove(vault.Retrieve(Resource, "user")); }
        catch (Exception exception) when (exception.HResult == unchecked((int)0x80070490)) { }
    }
}
