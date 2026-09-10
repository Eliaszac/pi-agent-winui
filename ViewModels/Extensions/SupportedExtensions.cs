using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Extensions;

public static class SupportedExtensions
{
    public static ExtensionDefinition Permissions { get; } = new(
        "Permission Modes", "GeorgeDong32", "2.6.3",
        "Control when Pi asks before changing files or running commands. Switch between manual approval, planning, automatic review, and bypass from the conversation input.",
        "Ask prompts for changes and mutating commands. Plan restricts writes to its plan file and limits commands to a read-only allowlist. Auto reviews tool actions using the extension's rules and a separate model. Bypass skips approval prompts. Existing rules and model profiles still apply. These are agent policies, not an operating-system sandbox.",
        PermissionModesSupport.InstallCommand,
        "Auto uses a separate approval model, defaulting to anthropic/claude-haiku-4-5. Choose a model you have connected in ~/.pi/agent/permission-modes.json. For example:",
        "{\n  \"classifier\": {\n    \"model\": \"openai-codex/gpt-5.5\",\n    \"failClosed\": true\n  }\n}",
        new("https://github.com/GeorgeDong32/pi-permission-modes#readme"),
        new("https://github.com/GeorgeDong32/pi-permission-modes"),
        () => PermissionModesSupport.GetInstallationState());

    public static ExtensionDefinition AutomaticTitles { get; } = new(
        "Automatic titles", "patlux", "0.1.1",
        "Names new conversations after their first completed run. Titles you rename manually stay yours.",
        "Generates a short title from a bounded excerpt of user and assistant messages. Pi Agent shows the title in the sidebar and conversation header, and preserves manual renames across restarts. Naming makes a separate model request and uses that provider's credentials and usage allowance.",
        AutoSessionNameSupport.InstallCommand,
        "The naming model defaults to openai-codex/gpt-5.4-mini, independently of the chat dropdown. Choose a connected model in ~/.pi/agent/extensions/auto-session-name.json. For example:",
        "{\n  \"provider\": \"openai-codex\",\n  \"model\": \"gpt-5.5\"\n}",
        new("https://github.com/patlux/pi-auto-session-name#readme"),
        new("https://github.com/patlux/pi-auto-session-name"), AutoSessionNameSupport.GetInstallationState);

    public static ExtensionDefinition Mcp { get; } = new(
        "MCP Adapter", "Nico Bailon", "2.10.0",
        "Connect Pi to MCP servers for additional tools and services. Recommended for use with Pi; no dedicated MCP interface in this app.",
        "A third-party extension maintained by Nico Bailon. It lets Pi discover and call tools from configured Model Context Protocol servers. Server configuration and authentication are managed by the extension, following its documentation. Pi Agent lists this as a recommendation and checks package installation only; it does not verify server connections or implement the extension's terminal panels.",
        "pi install npm:pi-mcp-adapter@2.10.0",
        "Configure your servers in ~/.pi/agent/mcp.json (or mcp.json in your custom PI_CODING_AGENT_DIR). Add each server's configuration under mcpServers using the upstream documentation, then restart Pi Agent. This empty structure does not connect any servers:",
        "{\n  \"mcpServers\": {}\n}",
        new("https://github.com/nicobailon/pi-mcp-adapter#readme"),
        new("https://github.com/nicobailon/pi-mcp-adapter"),
        () => McpAdapterSupport.GetInstallationState(), RecommendationOnly: true);

    public static ExtensionDefinition Search { get; } = new(
        "Pi Search", "heyhuynhgiabuu", PiSearchSupport.Version,
        "Search the web and look up documentation with Pi. Also enables web search for background research workers.",
        "A third-party extension providing web search, code search, documentation lookup and page fetching. Main conversations use its configured tools. Background research loads only web search, with bounded queries and the app's separate page reader. Installation does not verify provider availability or override permission rules.",
        PiSearchSupport.InstallCommand,
        "Basic search uses Exa's public MCP service without an API key. Optional provider keys and disabled tools can be configured in ~/.pi/pi-search.json following the upstream documentation. Restart Pi Agent after installation; new research workers use the installed supported version. Search queries are sent to the configured provider, whose limits and charges apply.",
        "",
        new("https://github.com/heyhuynhgiabuu/pi-search#readme"),
        new("https://github.com/heyhuynhgiabuu/pi-search"),
        () => PiSearchSupport.GetInstallationState());

    public static IReadOnlyList<ExtensionDefinition> All { get; } = [Permissions, AutomaticTitles, Search, Mcp];
}
