using System.Text.Json;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.Services.Conversations;

/// <summary>Projects authoritative messages and indexed streaming deltas without copying session persistence.</summary>
public sealed class PiTranscript
{
    private readonly Dictionary<string, ChatEntry> entries = [];
    private readonly SortedDictionary<int, string> textBlocks = [];
    private string? activeAssistant;
    private readonly Dictionary<string, JsonElement> toolInputs = [];

    public IReadOnlyList<ChatEntry> Load(JsonElement messages)
    {
        entries.Clear();
        activeAssistant = null;
        textBlocks.Clear();
        toolInputs.Clear();
        if (messages.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Pi returned an invalid message list.");
        foreach (var message in messages.EnumerateArray()) ApplyMessage(message, false);
        return entries.Values.ToArray();
    }

    public ChatEntry? Apply(JsonElement packet)
    {
        var type = PiJson.Text(packet, "type");
        if (type is "message_start" or "message_end") return ApplyMessage(PiJson.Field(packet, "message"), type == "message_start");
        if (type == "message_update")
        {
            var update = PiJson.Field(packet, "assistantMessageEvent");
            var deltaType = PiJson.Text(update, "type");
            if (deltaType is not ("text_delta" or "text_end")) return null;
            activeAssistant ??= "assistant:" + Guid.NewGuid().ToString("N");
            var index = PiJson.Number(update, "contentIndex");
            textBlocks.TryGetValue(index, out var current);
            textBlocks[index] = deltaType == "text_end" ? PiJson.Text(update, "content") : current + PiJson.Text(update, "delta");
            var entry = new ChatEntry(activeAssistant, "Pi", string.Join("\n\n", textBlocks.Values), Status: "Responding", IsAssistant: true, IsComplete: false);
            entries[entry.Id] = entry;
            return entry;
        }
        if (type is "tool_execution_start" or "tool_execution_update" or "tool_execution_end")
        {
            var id = "tool:" + PiJson.Text(packet, "toolCallId");
            if (id == "tool:") throw new InvalidDataException("Pi sent a tool event without an identifier.");
            entries.TryGetValue(id, out var previous);
            var args = PiJson.Field(packet, "args");
            if (args.ValueKind == JsonValueKind.Object) toolInputs[id] = args.Clone();
            if (args.ValueKind != JsonValueKind.Object) toolInputs.TryGetValue(id, out args);
            var details = args.ValueKind == JsonValueKind.Object ? args.GetRawText() : previous?.Details ?? "";
            var result = PiJson.Field(packet, type == "tool_execution_update" ? "partialResult" : "result");
            var text = type == "tool_execution_start" ? "" : PiJson.Content(PiJson.Field(result, "content"));
            var entry = new ChatEntry(id, PiJson.Text(packet, "toolName"), text, details,
                type == "tool_execution_end" ? (PiJson.Flag(packet, "isError") ? "Failed" : "Completed") : "Running", true,
                FileChange: type == "tool_execution_end" && !PiJson.Flag(packet, "isError")
                    ? FileChangeParser.Parse(PiJson.Text(packet, "toolName"), args, PiJson.Field(result, "details")) : null,
                ToolTokens: type == "tool_execution_end" ? PiTokenUsage.Read(PiJson.Field(result, "usage")) ?? previous?.ToolTokens : null,
                Images: ComputerUseSupport.IsTool(PiJson.Text(packet, "toolName")) || PiJson.Text(packet, "toolName").StartsWith("embedded_browser_", StringComparison.Ordinal) ? PiImageContent.Read(PiJson.Field(result, "content")) : null,
                Diagnostics: type == "tool_execution_end" && !PiJson.Flag(packet, "isError")
                    ? LspDiagnosticsParser.Parse(PiJson.Text(packet, "toolName"), args, PiJson.Field(result, "details")) : null);
            entries[id] = entry;
            return entry;
        }
        return null;
    }

    private ChatEntry? ApplyMessage(JsonElement message, bool starting)
    {
        var role = PiJson.Text(message, "role");
        if (role == "assistant" && PiJson.Field(message, "content") is { ValueKind: JsonValueKind.Array } content)
            foreach (var block in content.EnumerateArray())
                if (PiJson.Text(block, "type") == "toolCall") toolInputs["tool:" + PiJson.Text(block, "id")] = PiJson.Field(block, "arguments").Clone();
        if (role.Length == 0) throw new InvalidDataException("Pi sent a message without a role.");
        var timestamp = PiJson.Field(message, "timestamp");
        var id = role == "toolResult" ? "tool:" + PiJson.Text(message, "toolCallId")
            : role + ":" + (timestamp.ValueKind == JsonValueKind.Number ? timestamp.GetRawText() : Guid.NewGuid().ToString("N"));
        if (role == "assistant")
        {
            if (starting)
            {
                activeAssistant = id;
                textBlocks.Clear();
                var blocks = PiJson.Field(message, "content");
                if (blocks.ValueKind == JsonValueKind.Array)
                    for (var i = 0; i < blocks.GetArrayLength(); i++)
                        if (PiJson.Text(blocks[i], "type") == "text") textBlocks[i] = PiJson.Text(blocks[i], "text");
            }
            else if (activeAssistant is not null) id = activeAssistant;
        }
        if (role == "custom" && !PiJson.Flag(message, "display")) return null;
        entries.TryGetValue(id, out var previous);
        var text = PiJson.Content(PiJson.Field(message, "content"), includeImagePlaceholder: role != "user");
        if (role is "compactionSummary" or "branchSummary") text = PiJson.Text(message, "summary");
        if (role == "bashExecution") text = PiJson.Text(message, "output");
        toolInputs.TryGetValue(id, out var toolInput);
        var entry = new ChatEntry(id, role switch { "user" => "You", "assistant" => "Pi", "toolResult" => PiJson.Text(message, "toolName"), _ => "Session" },
            text, previous?.Details ?? (toolInput.ValueKind == JsonValueKind.Object ? toolInput.GetRawText() : ""), starting && role == "assistant" ? "Responding" : PiJson.Text(message, "stopReason") switch
            {
                "error" => "Failed: " + PiJson.Text(message, "errorMessage"),
                "aborted" => "Stopped",
                _ => role == "toolResult" ? (PiJson.Flag(message, "isError") ? "Failed" : "Completed") : ""
            }, role is "toolResult" or "bashExecution", IsUser: role == "user", IsAssistant: role == "assistant",
            IsComplete: role != "assistant" || !starting,
            FileChange: role == "toolResult" && !PiJson.Flag(message, "isError")
                ? FileChangeParser.Parse(PiJson.Text(message, "toolName"), toolInput, PiJson.Field(message, "details")) ?? previous?.FileChange : null,
            ToolTokens: role == "toolResult" ? PiTokenUsage.Read(PiJson.Field(message, "usage")) ?? previous?.ToolTokens : null,
            Images: role == "user" || (role == "toolResult" && (ComputerUseSupport.IsTool(PiJson.Text(message, "toolName")) || PiJson.Text(message, "toolName").StartsWith("embedded_browser_", StringComparison.Ordinal)))
                ? PiImageContent.Read(PiJson.Field(message, "content")) : null,
            Diagnostics: role == "toolResult" && !PiJson.Flag(message, "isError")
                ? LspDiagnosticsParser.Parse(PiJson.Text(message, "toolName"), toolInput, PiJson.Field(message, "details")) : null);
        entries[id] = entry;
        if (role == "assistant" && !starting) { activeAssistant = null; textBlocks.Clear(); }
        return entry;
    }
}
