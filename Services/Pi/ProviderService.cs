using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Runs bounded management operations without a conversation, transcript, or model request.</summary>
public sealed class ProviderService(Func<PiRpcClient> clientFactory, string workingDirectory) : IProviderService, IAsyncDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    public event Action? CredentialsChanged;

    public async Task<IReadOnlyList<PiProvider>> RunAsync(string action, string? provider = null, string? method = null,
        Func<JsonElement, CancellationToken, Task<string?>>? prompt = null, Action<JsonElement>? notify = null,
        CancellationToken cancellationToken = default)
    {
        if (action is not ("list" or "login" or "logout")) throw new ArgumentException("Unsupported provider operation.");
        if (action != "list" && string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("Choose a provider.");
        if (action == "login" && method is not ("oauth" or "api_key")) throw new ArgumentException("Choose an authentication method.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        linked.CancelAfter(action == "login" ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(2));
        var token = linked.Token;
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(workingDirectory);
            await using var client = clientFactory();
            var events = Channel.CreateUnbounded<JsonElement>(new() { SingleReader = true, SingleWriter = true });
            client.EventReceived += packet => events.Writer.TryWrite(packet);
            client.Faulted += exception => events.Writer.TryComplete(new IOException("The Providers connection ended. Refresh and try again."));
            await client.StartAsync(new(workingDirectory, "", ManageProviders: true), token).ConfigureAwait(false);
            var commands = await client.RequestAsync("get_commands", cancellationToken: token).ConfigureAwait(false);
            var list = PiJson.Field(PiJson.Field(commands, "data"), "commands");
            if (list.ValueKind != JsonValueKind.Array || !list.EnumerateArray().Any(item => PiJson.Text(item, "name") == "pi-gui-providers"))
                throw new InvalidOperationException("The Providers integration couldn't load. Update Pi and restart Pi desktop.");
            var request = new JsonObject { ["action"] = action, ["provider"] = provider, ["method"] = method };
            var processing = ReadResultAsync();
            try
            {
                var command = client.RequestAsync("prompt", new() { ["message"] = "/pi-gui-providers " + request.ToJsonString() }, token,
                    timeout: TimeSpan.FromMinutes(11));
                try
                {
                    var first = await Task.WhenAny(command, processing).ConfigureAwait(false);
                    await first.ConfigureAwait(false);
                    await command.ConfigureAwait(false);
                    return await processing.ConfigureAwait(false);
                }
                finally
                {
                    linked.Cancel();
                    try { await command.ConfigureAwait(false); } catch (Exception) { /* Observed by the operation above. */ }
                }
            }
            finally
            {
                linked.Cancel();
                try { await processing.ConfigureAwait(false); } catch (Exception) { /* The primary operation reports the failure. */ }
            }

            async Task<IReadOnlyList<PiProvider>> ReadResultAsync()
            {
                await foreach (var packet in events.Reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    if (PiJson.Text(packet, "type") != "extension_ui_request") continue;
                    if (PiJson.Text(packet, "method") == "input")
                    {
                        using var document = JsonDocument.Parse(PiJson.Text(packet, "title") ?? "{}");
                        if (PiJson.Field(document.RootElement, "piGuiProviders").GetInt32() != 1) throw new InvalidDataException("Unexpected authentication prompt.");
                        // Do not block the event reader: browser callbacks can dismiss a pending manual-code prompt.
                        _ = ReplyAsync(PiJson.Text(packet, "id")!, document.RootElement.Clone());
                    }
                    else if (PiJson.Text(packet, "method") == "notify")
                    {
                        var message = PiJson.Text(packet, "message");
                        if (message is null || !message.StartsWith('{')) continue;
                        using var document = JsonDocument.Parse(message);
                        var data = document.RootElement;
                        if (!data.TryGetProperty("piGuiProviders", out var version) || version.GetInt32() != 1) continue;
                        switch (PiJson.Text(data, "kind"))
                        {
                            case "changed": CredentialsChanged?.Invoke(); break;
                            case "error": throw new InvalidOperationException(PiJson.Text(data, "message"));
                            case "result": return PiProviderParser.Parse(data.GetProperty("providers"));
                            default: notify?.Invoke(data.Clone()); break;
                        }
                    }
                }
                throw new IOException("Pi didn't return provider information.");
            }
            async Task ReplyAsync(string id, JsonElement data)
            {
                try
                {
                    var value = prompt is null ? null : await prompt(data, token).ConfigureAwait(false);
                    await client.ReplyToExtensionAsync(id, value is null ? new() { ["cancelled"] = true } : new() { ["value"] = value }, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception) { events.Writer.TryComplete(new IOException("The authentication prompt couldn't be completed.")); }
            }
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        await gate.WaitAsync().ConfigureAwait(false);
        gate.Release();
    }
}
