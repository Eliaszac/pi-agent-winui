using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

public sealed class PiResearchRunner(Func<PiRpcClient> clientFactory, string storageDirectory) : IResearchRunner
{
    public async Task<string> RunAsync(ResearchTask task, CancellationToken cancellationToken)
    {
        await using var client = clientFactory();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Faulted += exception => finished.TrySetException(exception);
        client.EventReceived += packet =>
        {
            var type = PiJson.Text(packet, "type");
            if (type == "agent_settled") finished.TrySetResult();
            if (type == "extension_error") finished.TrySetException(new InvalidOperationException("The worker's read-only tools could not load."));
            if (type == "extension_ui_request") finished.TrySetException(new InvalidOperationException("The worker requested interactive input. Its answer could not be completed independently."));
        };
        await client.StartAsync(new PiLaunchRequest(task.Directory, Path.Combine(storageDirectory, task.Id + ".jsonl"),
            ResearchWorker: true, Provider: task.Provider, Model: task.Model, Effort: task.Effort), cancellationToken);
        var state = await client.RequestAsync("get_state", cancellationToken: cancellationToken);
        var model = PiJson.Field(PiJson.Field(state, "data"), "model");
        if (PiJson.Text(model, "provider") != task.Provider || PiJson.Text(model, "id") != task.Model)
            throw new InvalidOperationException("The selected model is unavailable to this worker. No fallback model was used.");
        await client.RequestAsync("prompt", new JsonObject { ["message"] = "Research question:\n" + task.Question }, cancellationToken);
        await finished.Task.WaitAsync(cancellationToken);
        var history = await client.RequestAsync("get_messages", cancellationToken: cancellationToken);
        var messages = PiJson.Field(PiJson.Field(history, "data"), "messages");
        var entries = new PiTranscript().Load(messages);
        var answer = entries.LastOrDefault(entry => entry.IsAssistant && !string.IsNullOrWhiteSpace(entry.Text));
        if (answer is null) throw new InvalidOperationException("The worker finished without an answer.");
        if (answer.Status.StartsWith("Failed", StringComparison.Ordinal)) throw new InvalidOperationException(answer.Text);
        return answer.Text;
    }
}
