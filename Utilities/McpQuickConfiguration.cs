using System.Text.Json.Nodes;

namespace PiAgentGui.Utilities;

/// <summary>Curated HTTP definitions; credentials remain outside the generated configuration.</summary>
public static class McpQuickConfiguration
{
    public static string BuildObsidian(string folder)
    {
        if (!Path.IsPathFullyQualified(folder) || !Directory.Exists(Path.Combine(folder, ".obsidian")))
            throw new ArgumentException("Choose an existing Obsidian vault containing an .obsidian folder.");
        return new JsonObject { ["mcpServers"] = new JsonObject { ["obsidian"] = new JsonObject
        {
            ["command"] = "npx.cmd",
            ["args"] = new JsonArray("-y", "obsidian-mcp@2", "serve", "--vault", "obsidian=" + Path.GetFullPath(folder))
        } } }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
    public static string Build(string id) => new JsonObject
    {
        ["mcpServers"] = new JsonObject { [id] = Definition(id) }
    }.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    private static JsonObject Definition(string id)
    {
        var url = id switch
        {
            "obsidian" => throw new ArgumentException("Choose a vault folder for Obsidian."),
            "atlassian" => "https://mcp.atlassian.com/v2/mcp",
            "github" => "https://api.githubcopilot.com/mcp/",
            "linear" => "https://mcp.linear.app/mcp",
            "notion" => "https://mcp.notion.com/mcp",
            "supabase" => "https://mcp.supabase.com/mcp",
            _ => throw new ArgumentException("Unknown quick integration.", nameof(id))
        };
        var server = new JsonObject { ["url"] = url, ["auth"] = "oauth" };
        if (id is "obsidian" or "github")
        {
            server["auth"] = "bearer";
            server["bearerTokenStore"] = true;
        }
        return server;
    }

    public static string Guidance(string id) => id switch
    {
        "obsidian" => "Uses the local obsidian-mcp stdio server with your selected vault. No Obsidian plugin, API key, or running Obsidian app is required. Node.js/npm must be installed; npx downloads the 2.x package on first use. The server can read and change files in this vault. Restart Pi desktop after saving. Existing HTTP configurations are preserved; use a different server name if you want both.",
        "github" => "Set the Windows environment variable GITHUB_MCP_TOKEN to a GitHub personal access token with your chosen repository permissions before restarting Pi desktop. The existing GitHub PR connection is separate and is not reused. Only the environment variable name is saved here.",
        "supabase" => "After saving, restart Pi desktop and authenticate through the adapter's /mcp-auth supabase command in a local conversation. Consider adding project_ref and read_only=true query parameters to scope access to your development project.",
        _ => $"After saving, restart Pi desktop and authenticate through the adapter's /mcp-auth {id} command in a local conversation. Your organization may require consent or administrator approval."
    };
}
