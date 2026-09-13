using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using PiAgentGui.Models.GitHub;

namespace PiAgentGui.Services.GitHub;

public sealed class GitHubApi(HttpClient http)
{
    public async Task<JsonDocument> PostLoginAsync(string endpoint, Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/" + endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(fields);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new IOException("GitHub sign-in is unavailable. Please try again.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public async Task<GitHubPullRequest?> FindPullRequestAsync(GitHubBranch branch, string token, CancellationToken cancellationToken)
    {
        using var repository = await GetAsync($"repos/{branch.FullName}", token, cancellationToken);
        var targets = new List<string> { branch.FullName };
        if (repository.RootElement.TryGetProperty("parent", out var parent) && parent.TryGetProperty("full_name", out var name)
            && name.GetString() is { } parentName && Utilities.GitHubRemote.Parse("https://github.com/" + parentName, branch.Branch) is not null)
            targets.Insert(0, parentName);
        foreach (var target in targets)
        {
            using var pulls = await GetAsync($"repos/{target}/pulls?state=open&head={Uri.EscapeDataString(branch.Owner + ":" + branch.Branch)}&sort=updated&direction=desc&per_page=100", token, cancellationToken);
            foreach (var pull in pulls.RootElement.EnumerateArray())
            {
                var head = pull.GetProperty("head");
                if (head.GetProperty("ref").GetString() != branch.Branch ||
                    !string.Equals(head.GetProperty("repo").GetProperty("full_name").GetString(), branch.FullName, StringComparison.OrdinalIgnoreCase)) continue;
                var number = pull.GetProperty("number").GetInt32();
                if (number > 0) return new(number, pull.GetProperty("title").GetString() ?? "Pull request", new Uri($"https://github.com/{target}/pull/{number}"),
                    pull.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object
                        && user.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.String ? login.GetString() : null,
                    pull.TryGetProperty("draft", out var draft) && draft.ValueKind is JsonValueKind.True or JsonValueKind.False ? draft.GetBoolean() : null,
                    pull.TryGetProperty("created_at", out var created) && created.ValueKind == JsonValueKind.String && created.TryGetDateTimeOffset(out var createdAt) ? createdAt : null,
                    pull.TryGetProperty("updated_at", out var updated) && updated.ValueKind == JsonValueKind.String && updated.TryGetDateTimeOffset(out var updatedAt) ? updatedAt : null);
            }
        }
        return null;
    }

    private async Task<JsonDocument> GetAsync(string path, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("PiAgentGui/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("Your GitHub session expired. Connect again.");
        if (response.StatusCode == HttpStatusCode.NotFound) throw new IOException("GitHub repository unavailable. Install the GitHub App on this repository and check your access.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new IOException("GitHub access is restricted or rate-limited. Check the app's repository permissions or retry later.");
        if (!response.IsSuccessStatusCode) throw new IOException("Couldn't check pull requests on GitHub. Please try again.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
