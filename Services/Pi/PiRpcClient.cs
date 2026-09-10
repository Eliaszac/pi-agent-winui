using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Services.Pi;

/// <summary>Correlates RPC replies while delivering events through a single ordered reader.</summary>
public sealed class PiRpcClient(IPiTransport transport, TimeSpan requestTimeout) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, (string Command, TaskCompletionSource<JsonElement> Completion, Action<JsonElement>? Apply)> pending = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task reader = Task.CompletedTask;
    private int started;
    private int disposed;
    private Exception? failure;
    public event Action<JsonElement>? EventReceived;
    public event Action<Exception>? Faulted;

    public async Task StartAsync(PiLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("The RPC client has already started.");
        await transport.StartAsync(request, cancellationToken).ConfigureAwait(false);
        reader = ReadAsync();
    }

    public async Task<JsonElement> RequestAsync(string command, JsonObject? arguments = null, CancellationToken cancellationToken = default,
        Action<JsonElement>? applyResponse = null, TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (failure is not null) throw new IOException("The Pi connection ended. Reconnect before sending another request.", failure);
        var id = Guid.NewGuid().ToString("N");
        var packet = arguments?.DeepClone().AsObject() ?? new JsonObject();
        packet["id"] = id;
        packet["type"] = command;
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = (command, completion, applyResponse);
        try
        {
            if (failure is not null) throw new IOException("The Pi connection ended.", failure);
            await transport.WriteLineAsync(packet.ToJsonString(), cancellationToken).ConfigureAwait(false);
            return await completion.Task.WaitAsync(timeout ?? requestTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            Fail(new TimeoutException("Pi did not acknowledge the request. Its acceptance is uncertain; the request will not be resent automatically.", exception));
            lifetime.Cancel();
            throw failure!;
        }
        finally { pending.TryRemove(id, out _); }
    }

    public Task ReplyToExtensionAsync(string id, JsonObject response, CancellationToken cancellationToken = default)
    {
        var packet = response.DeepClone().AsObject();
        packet["id"] = id;
        packet["type"] = "extension_ui_response";
        return transport.WriteLineAsync(packet.ToJsonString(), cancellationToken);
    }

    private async Task ReadAsync()
    {
        try
        {
            await foreach (var line in transport.ReadLinesAsync(lifetime.Token).ConfigureAwait(false))
            {
                using var document = JsonDocument.Parse(line);
                var packet = document.RootElement;
                if (packet.ValueKind != JsonValueKind.Object || !packet.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Pi sent an invalid protocol envelope.");
                if (type.GetString() != "response") { EventReceived?.Invoke(packet.Clone()); continue; }
                if (!packet.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !pending.TryRemove(id.GetString()!, out var request)) continue;
                if (!packet.TryGetProperty("command", out var command) || command.ValueKind != JsonValueKind.String || command.GetString() != request.Command ||
                    !packet.TryGetProperty("success", out var success) || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    var exception = new InvalidDataException("Pi sent an invalid command response.");
                    request.Completion.TrySetException(exception);
                    throw exception;
                }
                if (success.GetBoolean())
                {
                    try
                    {
                        // Apply snapshots on the reader before any following event, avoiding stale-state races.
                        request.Apply?.Invoke(packet);
                        request.Completion.TrySetResult(packet.Clone());
                    }
                    catch (Exception exception) { request.Completion.TrySetException(exception); throw; }
                }
                else request.Completion.TrySetException(new PiCommandException(request.Command,
                    packet.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString()! : "Pi rejected the command."));
            }
            if (!lifetime.IsCancellationRequested) Fail(new IOException("Pi exited. Reconnect to reopen the saved session."));
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) { Fail(exception); }
        finally
        {
            foreach (var request in pending.Values) request.Completion.TrySetException(failure ?? new IOException("The Pi connection closed."));
            pending.Clear();
            await transport.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.CompareExchange(ref failure, exception, null) is null && disposed == 0) Faulted?.Invoke(exception);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        await reader.ConfigureAwait(false);
        await transport.DisposeAsync().ConfigureAwait(false);
        lifetime.Dispose();
    }
}
