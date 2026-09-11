using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Reads the extension's own persisted state; never implements permission decisions.</summary>
public sealed class PermissionModesIntegration(Action<ConversationUpdate> publish, Func<bool>? isConfigured = null,
    Func<JsonElement, bool>? isSupportedCommand = null)
{
    public bool Available { get; private set; }
    public bool DefaultApplied { get; private set; }
    private bool hasSavedMode;
    private string? currentMode;

    public async Task DiscoverAsync(PiRpcClient client, CancellationToken cancellationToken)
    {
        Available = false;
        DefaultApplied = false;
        publish(new() { ApprovalAvailable = false, HasApprovalModeUpdate = true, ApprovalStatus = "Install approval extension" });
        try
        {
            if (!(isConfigured?.Invoke() ?? PermissionModesSupport.IsGloballyConfigured())) return;
            if (!await HasCommandAsync(client, cancellationToken).ConfigureAwait(false))
            {
                publish(new() { ApprovalStatus = "Installed extension not loaded · view setup" });
                return;
            }
            await RefreshAsync(client, cancellationToken).ConfigureAwait(false);
            Available = true;
            if (!hasSavedMode)
            {
                await SetAsync(client, "auto", cancellationToken).ConfigureAwait(false);
                if (currentMode != "auto") throw new InvalidDataException("Pi did not confirm the default Auto mode.");
                DefaultApplied = true;
            }
            publish(new() { ApprovalAvailable = true, ApprovalStatus = "Approval mode" });
        }
        catch (Exception exception) when (exception is PiCommandException or IOException or JsonException or UnauthorizedAccessException)
        {
            Available = false;
            publish(new() { ApprovalStatus = "Approval check failed: " + exception.Message });
        }
    }

    public Task RefreshAsync(PiRpcClient client, CancellationToken cancellationToken = default) =>
        client.RequestAsync("get_entries", cancellationToken: cancellationToken, applyResponse: packet =>
        {
            var entries = PiJson.Field(PiJson.Field(packet, "data"), "entries");
            currentMode = PermissionModesSupport.ReadMode(entries);
            hasSavedMode = entries.EnumerateArray().Any(entry => PiJson.Text(entry, "type") == "custom" && PiJson.Text(entry, "customType") == "modes");
            publish(new() { HasApprovalModeUpdate = true, ApprovalMode = currentMode });
        });

    public async Task SetAsync(PiRpcClient client, string mode, CancellationToken cancellationToken)
    {
        if (!Available || !PermissionModesSupport.Modes.Contains(mode)) throw new InvalidOperationException("The approval extension is unavailable.");
        if (!await HasCommandAsync(client, cancellationToken).ConfigureAwait(false))
        {
            Available = false;
            publish(new() { ApprovalAvailable = false, HasApprovalModeUpdate = true, ApprovalStatus = "Extension unavailable · view setup" });
            throw new InvalidOperationException("The approval extension is no longer loaded. Restart Pi desktop after checking setup.");
        }
        await client.RequestAsync("prompt", new JsonObject { ["message"] = "/mode " + mode }, cancellationToken).ConfigureAwait(false);
        await RefreshAsync(client, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> HasCommandAsync(PiRpcClient client, CancellationToken cancellationToken)
    {
        var response = await client.RequestAsync("get_commands", cancellationToken: cancellationToken).ConfigureAwait(false);
        var commands = PiJson.Field(PiJson.Field(response, "data"), "commands");
        return commands.ValueKind == JsonValueKind.Array && commands.EnumerateArray().Any(isSupportedCommand ?? PermissionModesSupport.IsSupportedCommand);
    }
}
