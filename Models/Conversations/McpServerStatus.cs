namespace PiAgentGui.Models.Conversations;

/// <summary>A sanitized adapter-owned runtime status; no connection definitions or credentials.</summary>
public sealed record McpServerStatus(string Name, string State, int ToolCount, int? ResourceCount)
{
    public string Status => State switch
    {
        "connected" => "Connected", "cached" => "Cached", "failed" => "Failed",
        "needs-auth" => "Needs authentication", "disabled" => "Disabled", _ => "Not connected"
    };
    public string Counts => $"{ToolCount} tools" + (ResourceCount is { } count ? $" · {count} resources" : "");
    public string Explanation => State switch
    {
        "connected" => "Connected in this conversation.",
        "cached" => "Tool metadata is cached. The server connects when a tool needs it.",
        "failed" => "The adapter reported a recent connection failure. Check the server configuration and try the action again.",
        "needs-auth" => "This server needs authentication through the MCP adapter.",
        "disabled" => "Disabled in the adapter configuration.",
        _ => "Not connected yet. Lazy servers normally connect on first use."
    };
}
