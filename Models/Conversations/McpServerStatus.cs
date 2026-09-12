namespace PiAgentGui.Models.Conversations;

/// <summary>A sanitized adapter-owned runtime status; no connection definitions or credentials.</summary>
public sealed record McpServerStatus(string Name, string State, int ToolCount, int? ResourceCount, string? Error = null)
{
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string Status => State switch
    {
        "configured" => "Configured · restart to load", "connected" => "Connected", "cached" => "Cached", "failed" => "Failed",
        "needs-auth" => "Needs authentication", "disabled" => "Disabled", _ => "Not connected"
    };
    public string Counts => State == "configured" ? "Tools not loaded yet" : $"{ToolCount} tools" + (ResourceCount is { } count ? $" · {count} resources" : "");
    public string Explanation => !string.IsNullOrWhiteSpace(Error) ? Error : State switch
    {
        "connected" => "Connected in this conversation.",
        "cached" => "Tool metadata is cached. The server connects when a tool needs it.",
        "configured" => "Saved globally but not reported by this conversation. Restart Pi desktop to load it. Authentication may still be required.",
        "failed" => "The adapter reported a connection failure without an error detail. Check configuration and authentication; errors returned by MCP tools appear here when available.",
        "needs-auth" => "This server needs authentication through the MCP adapter.",
        "disabled" => "Disabled in the adapter configuration.",
        _ => "Not connected yet. Lazy servers normally connect on first use."
    };
}
