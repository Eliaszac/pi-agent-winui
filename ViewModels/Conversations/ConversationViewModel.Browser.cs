using System.Text.Json;
using System.Text.Json.Nodes;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    public Func<JsonElement, Task<JsonObject>>? BrowserRequestHandler { get; set; }
    public Func<Uri, Task>? OpenBrowser { get; set; }

    private async Task HandleBrowserRequestAsync(JsonElement packet)
    {
        JsonObject result;
        try
        {
            using var request = JsonDocument.Parse(PiJson.Text(packet, "placeholder"));
            result = BrowserRequestHandler is { } handler ? await handler(request.RootElement.Clone())
                : throw new InvalidOperationException("The browser panel is unavailable for this conversation.");
        }
        catch (Exception error) { result = new() { ["error"] = error.Message }; }
        try { await session.ReplyAsync(PiJson.Text(packet, "id"), new() { ["value"] = result.ToJsonString() }); }
        catch (Exception error) { ReportAttachmentError("Browser reply failed: " + error.Message); }
    }
}
