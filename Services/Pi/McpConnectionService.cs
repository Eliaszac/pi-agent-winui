using System.Text.Json;
using System.Threading.Channels;
using PiAgentGui.Models.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Pi;

/// <summary>Checks one MCP server through an isolated, disposable Pi runtime.</summary>
public sealed class McpConnectionService(Func<PiRpcClient> clientFactory, string workingDirectory, CancellationToken shutdown = default)
{
    public async Task<McpConnectionResult> RunAsync(string server, bool login, Action<string> progress,
        Func<JsonElement, CancellationToken, Task<string?>> prompt, CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(shutdown, cancellationToken);
        lifetime.CancelAfter(TimeSpan.FromMinutes(login ? 10 : 2));
        var token = lifetime.Token;
        Directory.CreateDirectory(workingDirectory);
        await using var client = clientFactory();
        var events = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(128) { SingleReader = true, SingleWriter = true });
        client.EventReceived += packet => { if (!events.Writer.TryWrite(packet)) events.Writer.TryComplete(new IOException("Too many MCP status messages.")); };
        client.Faulted += _ => events.Writer.TryComplete(new IOException("The MCP connection process stopped."));
        await client.StartAsync(new(workingDirectory, "", ManageMcpServer: server), token);
        var inventory = await client.RequestAsync("get_commands", cancellationToken: token);
        var commands = PiJson.Field(PiJson.Field(inventory, "data"), "commands");
        if (commands.ValueKind != JsonValueKind.Array || !commands.EnumerateArray().Any(row => PiJson.Text(row, "name") == "pi-gui-mcp-manage"))
            throw new InvalidOperationException("MCP management could not load. Check that Pi and MCP Adapter are installed and up to date.");
        var read = ReadAsync();
        var request = client.RequestAsync("prompt", new() { ["message"] = "/pi-gui-mcp-manage " + (login ? "login" : "connect") }, token, timeout: TimeSpan.FromMinutes(11));
        try
        {
            await await Task.WhenAny(request, read);
            await request;
            return await read;
        }
        catch (OperationCanceledException) when (!shutdown.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(login
                ? "MCP sign-in timed out after 10 minutes. Try signing in again."
                : "MCP connection timed out after 2 minutes. For a local server, check its command and Node.js installation. The first npm download may take longer; retry once it finishes.");
        }
        finally
        {
            lifetime.Cancel();
            try { await request; } catch (Exception) { }
            try { await read; } catch (Exception) { }
        }

        async Task<McpConnectionResult> ReadAsync()
        {
            await foreach (var packet in events.Reader.ReadAllAsync(token))
            {
                if (PiJson.Text(packet, "type") != "extension_ui_request") continue;
                var method = PiJson.Text(packet, "method");
                if (method == "input") { _ = ReplyAsync(packet); continue; }
                if (method != "notify") continue;
                var text = PiJson.Text(packet, "message");
                if (!text.StartsWith('{')) continue;
                using var data = JsonDocument.Parse(text);
                var root = data.RootElement;
                if (PiJson.Number(root, "piGuiMcp") != 1) continue;
                var message = ConversationErrors.Describe(PiJson.Text(root, "message")).Message;
                switch (PiJson.Text(root, "kind"))
                {
                    case "progress": progress(message); break;
                    case "error": throw new InvalidOperationException(message);
                    case "result": return new(PiJson.Field(root, "connected").ValueKind == JsonValueKind.True,
                        PiJson.Text(root, "state"), (int)PiJson.Number(root, "toolCount"), message);
                }
            }
            throw new IOException("No MCP result was received.");
        }
        async Task ReplyAsync(JsonElement packet)
        {
            try
            {
                var answer = await prompt(packet, token);
                await client.ReplyToExtensionAsync(PiJson.Text(packet, "id"), answer is null ? new() { ["cancelled"] = true } : new() { ["value"] = answer }, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception) { events.Writer.TryComplete(new IOException("The authentication prompt could not be completed.")); }
        }
    }
}
