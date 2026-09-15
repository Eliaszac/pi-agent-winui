using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    private CancellationTokenSource githubWriteLifetime = new();
    private readonly HashSet<string> githubWriteRequests = [];

    private async Task HandleGitHubWriteAsync(JsonElement packet)
    {
        var id = PiJson.Text(packet, "id");
        if (!githubWriteRequests.Add(id)) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(githubWriteLifetime.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        JsonObject result;
        try
        {
            var input = PiJson.Text(packet, "placeholder");
            if (input.Length > 400000) throw new IOException("GitHub request exceeds the size limit.");
            var request = JsonSerializer.Deserialize<GitHubWriteRequest>(input, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
                ?? throw new IOException("Invalid GitHub request.");
            var github = GitHub ?? throw new IOException("Connect GitHub in Settings → Integrations.");
            result = await github.WriteAsync(request, GetGitHubRepositoryAsync, ApproveAsync, timeout.Token);
        }
        catch (OperationCanceledException) { result = new() { ["error"] = "GitHub operation cancelled or approval expired." }; }
        catch (Exception error) { result = new() { ["error"] = error.Message }; }
        try { if (!disposed) await session.ReplyAsync(id, new() { ["value"] = result.ToJsonString() }); }
        catch (Exception error) { if (!disposed) dispatcher.Post(() => ReportError(error)); }

        async Task<bool> ApproveAsync(string preview, CancellationToken cancellation)
        {
            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ExtensionPromptViewModel? prompt = null;
            using var registration = cancellation.Register(() => answer.TrySetCanceled(cancellation));
            dispatcher.Post(() =>
            {
                if (disposed || cancellation.IsCancellationRequested) { answer.TrySetCanceled(cancellation); return; }
                prompt = new(new ExtensionPrompt("github-write:" + id, "select", "Allow GitHub change? " + preview, "", ["Allow", "Block"], "", null),
                    (_, response) => { answer.TrySetResult(response["value"]?.GetValue<string>() == "Allow"); return Task.CompletedTask; },
                    RemovePrompt, ReportError, Target?.Label ?? "Local");
                Prompts.Add(prompt);
                NotifyState();
            });
            try { return await answer.Task; }
            finally { dispatcher.Post(() => { if (prompt is not null) RemovePrompt(prompt); }); }
        }
    }
}
