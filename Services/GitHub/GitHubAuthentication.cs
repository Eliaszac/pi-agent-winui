using PiAgentGui.Configuration;
using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Services.GitHub;

public sealed class GitHubAuthentication(GitHubOptions options, GitHubApi api, IGitHubCredentialStore credentials)
{
    public GitHubToken? Load()
    {
        var token = credentials.Read();
        if (token is null || token.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30)
            || token.RefreshToken is not null && token.RefreshExpiresAt > DateTimeOffset.UtcNow.AddSeconds(30)) return token;
        credentials.Clear();
        return null;
    }

    public void Disconnect() => credentials.Clear();

    public async Task<GitHubToken> RefreshAsync(GitHubToken token, CancellationToken cancellationToken)
    {
        if (token.RefreshToken is null || token.RefreshExpiresAt <= DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("Your GitHub session expired. Connect again.");
        using var response = await api.PostLoginAsync("oauth/access_token", new()
        {
            ["client_id"] = options.ClientId, ["grant_type"] = "refresh_token", ["refresh_token"] = token.RefreshToken
        }, cancellationToken);
        var root = response.RootElement;
        if (!root.TryGetProperty("access_token", out var access) || string.IsNullOrWhiteSpace(access.GetString()))
            throw new UnauthorizedAccessException("Your GitHub session expired. Connect again.");
        var refreshed = Utilities.GitHubTokenParser.Read(root);
        cancellationToken.ThrowIfCancellationRequested();
        return refreshed;
    }

    public void Save(GitHubToken token) => credentials.Save(token);

    public async Task<GitHubDeviceCode> BeginAsync(CancellationToken cancellationToken)
    {
        if (!options.IsConfigured) throw new IOException("GitHub isn't configured in this build. Add the GitHub App client ID to GitHubApp.json and rebuild.");
        using var result = await api.PostLoginAsync("device/code", new() { ["client_id"] = options.ClientId }, cancellationToken);
        var root = result.RootElement;
        if (root.TryGetProperty("error", out _)) throw new IOException("GitHub couldn't start sign-in. Check the client ID and enable Device flow in the GitHub App settings.");
        return new(root.GetProperty("device_code").GetString()!, root.GetProperty("user_code").GetString()!,
            Math.Max(5, root.GetProperty("interval").GetInt32()), DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()));
    }

    public async Task<GitHubToken> CompleteAsync(GitHubDeviceCode code, CancellationToken cancellationToken)
    {
        var interval = code.Interval;
        while (DateTimeOffset.UtcNow < code.ExpiresAt)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
            using var result = await api.PostLoginAsync("oauth/access_token", new()
            {
                ["client_id"] = options.ClientId, ["device_code"] = code.DeviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            }, cancellationToken);
            var root = result.RootElement;
            if (root.TryGetProperty("access_token", out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                var token = Utilities.GitHubTokenParser.Read(root);
                cancellationToken.ThrowIfCancellationRequested();
                credentials.Save(token);
                return token;
            }
            var error = root.TryGetProperty("error", out var field) ? field.GetString() : null;
            if (error == "authorization_pending") continue;
            if (error == "slow_down") { interval += 5; continue; }
            throw new IOException(error == "access_denied" ? "GitHub sign-in was declined." : "GitHub sign-in expired or failed. Please try again.");
        }
        throw new IOException("The GitHub code expired. Please connect again.");
    }
}
