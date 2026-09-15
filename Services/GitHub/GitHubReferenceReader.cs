using System.Text;
using System.Text.Json;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.GitHub;

public sealed class GitHubReferenceReader(GitHubApi api)
{
    public async Task<IReadOnlyList<GitHubReference>> SearchAsync(string query, bool pulls, string repository, string token, CancellationToken cancellation)
    {
        if (GitHubRemote.Parse("https://github.com/" + repository, "HEAD") is null) throw new IOException("Invalid project repository.");
        if (GitHubReference.FromUrl(query) is { } direct)
        {
            if (!direct.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a PR or issue from this project's repository: " + repository);
            using var item = await api.GetAsync($"repos/{direct.Repository}/issues/{direct.Number}", token, cancellation);
            return [Read(item.RootElement, direct.Repository)];
        }
        if (query.Length > 256) throw new IOException("Use a shorter GitHub search.");
        var terms = query.Replace("\"", "").Replace("\\", "").Trim();
        var scoped = "repo:" + repository + (pulls ? " is:pr" : " is:issue") + (terms.Length > 0 ? " \"" + terms + "\"" : "");
        using var response = await api.GetAsync("search/issues?per_page=20&sort=updated&order=desc&q=" + Uri.EscapeDataString(scoped), token, cancellation);
        return response.RootElement.GetProperty("items").EnumerateArray().Select(item =>
        {
            var parsed = GitHubReference.FromUrl(PiJson.Text(item, "html_url")) ?? throw new IOException("Invalid GitHub result URL.");
            return Read(item, parsed.Repository);
        }).Where(item => item.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    private static GitHubReference Read(JsonElement item, string repository) => new(repository, item.GetProperty("number").GetInt32(), item.TryGetProperty("pull_request", out _), PiJson.Text(item, "title"), PiJson.Flag(item, "draft") ? "Draft" : PiJson.Text(item, "state"));

    public async Task<GitHubReference> FetchAsync(GitHubReference reference, string token, CancellationToken cancellation)
    {
        if (GitHubReference.FromUrl(reference.Url) is null) throw new IOException("Invalid GitHub reference.");
        var prefix = $"repos/{reference.Repository}";
        using var issue = await api.GetAsync($"{prefix}/issues/{reference.Number}", token, cancellation);
        var current = Read(issue.RootElement, reference.Repository);
        var content = new StringBuilder();
        Add("Description", PiJson.Text(issue.RootElement, "body"));
        using var comments = await api.GetAsync($"{prefix}/issues/{reference.Number}/comments?per_page=30", token, cancellation);
        foreach (var comment in comments.RootElement.EnumerateArray()) Add("Comment by " + PiJson.Text(PiJson.Field(comment, "user"), "login"), PiJson.Text(comment, "body"));
        if (current.IsPullRequest)
        {
            using var pr = await api.GetAsync($"{prefix}/pulls/{reference.Number}", token, cancellation);
            current = current with { State = PiJson.Flag(pr.RootElement, "merged") ? "Merged" : PiJson.Flag(pr.RootElement, "draft") ? "Draft" : PiJson.Text(pr.RootElement, "state") };
            Add("Revisions", "Base: " + PiJson.Text(PiJson.Field(pr.RootElement, "base"), "sha") + "\nHead: " + PiJson.Text(PiJson.Field(pr.RootElement, "head"), "sha"));
            foreach (var route in new[] { "reviews", "comments", "files" })
            {
                using var results = await api.GetAsync($"{prefix}/pulls/{reference.Number}/{route}?per_page=30", token, cancellation);
                foreach (var entry in results.RootElement.EnumerateArray())
                    Add(route == "files" ? PiJson.Text(entry, "filename") : route + " · " + PiJson.Text(PiJson.Field(entry, "user"), "login"),
                        route == "files" ? PiJson.Text(entry, "patch") is { Length: > 0 } patch ? patch : "Patch unavailable (possibly binary or too large)." : PiJson.Text(entry, "body"));
            }
        }
        content.Append("\nSnapshot fetched " + DateTimeOffset.UtcNow.ToString("O") + ". Limited to the first 30 entries per section and 40,000 characters; additional content may be omitted.");
        return current with { Content = content.ToString() };
        void Add(string title, string text)
        {
            var remaining = 40000 - content.Length;
            if (remaining <= 0) return;
            var block = "\n\n" + title + "\n" + text;
            content.Append(block.AsSpan(0, Math.Min(block.Length, remaining)));
        }
    }
}
