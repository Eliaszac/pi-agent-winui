using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.GitHub;

/// <summary>Prepares an exact proposal and revalidates it after native user approval.</summary>
public sealed class GitHubWriter(GitHubApi api)
{
    public async Task<JsonObject> ExecuteAsync(GitHubWriteRequest request, string token,
        Func<CancellationToken, Task<string>> repository, Func<string, CancellationToken, Task<bool>> approve,
        Action checkConnection, CancellationToken cancellation)
    {
        if (request.Number <= 0 || request.Action is not ("update_issue" or "comment_pr")) throw new IOException("Unsupported GitHub write request.");
        if (request.Title is { } title && (string.IsNullOrWhiteSpace(title) || title.Length > 256)) throw new IOException("Issue titles must contain 1–256 characters.");
        if (request.Body?.Length > 60000) throw new IOException("GitHub text is limited to 60,000 characters.");
        if (request.State is not (null or "open" or "closed")) throw new IOException("Issue state must be open or closed.");
        var comment = request.Action == "comment_pr";
        if (comment && (string.IsNullOrWhiteSpace(request.Body) || request.Title is not null || request.State is not null)) throw new IOException("PR comments require only a nonempty body.");
        if (!comment && request.Title is null && request.Body is null && request.State is null) throw new IOException("Provide an issue title, body, or state to update.");
        var repo = await repository(cancellation);
        if (GitHubReference.FromUrl($"https://github.com/{repo}/issues/{request.Number}") is null) throw new IOException("This project needs a valid GitHub origin remote.");
        var path = $"repos/{repo}/issues/{request.Number}";
        checkConnection();
        using var before = await api.GetAsync(path, token, cancellation);
        var item = before.RootElement;
        if (item.TryGetProperty("pull_request", out _) != comment) throw new IOException(comment ? "This number identifies an issue, not a pull request." : "This number identifies a pull request, not an issue.");
        var payload = new JsonObject();
        var url = $"https://github.com/{repo}/{(comment ? "pull" : "issues")}/{request.Number}";
        var preview = new StringBuilder().AppendLine(url).AppendLine(item.GetProperty("title").GetString()).AppendLine();
        if (comment) { payload["body"] = request.Body; preview.AppendLine("Post comment:").Append(request.Body); }
        else
        {
            foreach (var (field, value) in new[] { ("title", request.Title), ("body", request.Body), ("state", request.State) })
            {
                if (value is null) continue;
                payload[field] = value;
                preview.AppendLine(field + " — before:").AppendLine(item.GetProperty(field).GetString() ?? "")
                    .AppendLine(field + " — after:").AppendLine(value).AppendLine();
            }
        }
        if (!await approve(preview.ToString(), cancellation)) return new() { ["cancelled"] = true, ["message"] = "Blocked by the user. Nothing was sent to GitHub." };
        cancellation.ThrowIfCancellationRequested();
        if (!string.Equals(repo, await repository(cancellation), StringComparison.OrdinalIgnoreCase)) throw new IOException("The project's GitHub repository changed. Request approval again.");
        checkConnection();
        using var latest = await api.GetAsync(path, token, cancellation);
        if (latest.RootElement.TryGetProperty("pull_request", out _) != comment) throw new IOException("The GitHub item type changed. Request approval again.");
        if (!comment && new[] { "title", "body", "state", "updated_at" }.Any(field => item.GetProperty(field).GetRawText() != latest.RootElement.GetProperty(field).GetRawText()))
            throw new IOException("The issue changed while approval was pending. Read it again and request fresh approval; nothing was written.");
        cancellation.ThrowIfCancellationRequested();
        checkConnection();
        using var result = await api.WriteAsync(comment ? HttpMethod.Post : HttpMethod.Patch, path + (comment ? "/comments" : ""), payload, token, cancellation);
        if (comment && result.RootElement.TryGetProperty("id", out var id) && id.TryGetInt64(out var commentId)) url += "#issuecomment-" + commentId;
        return new() { ["success"] = true, ["url"] = url, ["message"] = comment ? "PR comment posted." : "Issue updated." };
    }
}
