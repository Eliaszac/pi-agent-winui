using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Extensions;

public static class SupportedExtensions
{
    public static ExtensionDefinition Docker(Docker.DockerPanelViewModel docker) => new(
        "Docker", "Pi desktop", "1.0.0",
        "Start and stop containers from your conversation. Detect Windows and WSL sources, connect saved SSH targets, and link containers to projects.",
        "A built-in Pi desktop integration using each source’s Docker CLI and current context. Docker must already be installed and its engine available. Unlinked containers appear in every project; linked containers appear only in their selected projects. Removing a source or disabling this extension leaves containers running. Windows and WSL detection may start WSL distributions. SSH uses the target’s existing connection settings.",
        "", "", "", new("https://docs.docker.com/engine/"), new("https://docs.docker.com/reference/cli/docker/"),
        () => (docker.Enabled ? "Enabled · Windows, WSL and SSH" : "Disabled · containers are unchanged", true),
        Bundled: true, ReadEnabled: () => docker.Enabled, WriteEnabled: docker.SetEnabledAsync,
        ToggleHint: "Shows the Docker panel. Turning off keeps sources, links and running containers.", Docker: docker);

    public static ExtensionDefinition Research(Services.Conversations.ResearchCoordinator coordinator) => new(
        "Background research", "Pi desktop", "1.0.0",
        "Let Pi research in parallel while you keep working. Review results in the conversation’s Research panel and add them to your prompt when ready.",
        "An optional built-in Pi desktop feature for local projects. Up to two independent, read-only workers can inspect files and read HTTPS pages. Install Pi Search for web search. Each task allows up to 12 search queries and 20 page reads. Workers use model requests and the provider’s usage allowance. Enabling applies next turn; disabling cancels active and queued tasks and hides the Research panel. Saved results and settings are kept. Results are never sent to the conversation automatically.",
        "", "", "", new("https://pi.dev/docs/latest/extensions"), new("https://pi.dev/docs/latest/extensions"),
        () => (coordinator.Enabled ? "Enabled · local projects" : "Disabled · saved results are kept", true),
        Bundled: true, ReadEnabled: () => coordinator.Enabled, WriteEnabled: coordinator.SetEnabledAsync);
    public static ExtensionDefinition Checkpoints { get; } = new(
        "Workspace checkpoints", "Pi desktop", "1.0.0",
        "Recover from unwanted agent edits. Review created, modified and deleted files, revert captured changes, and undo a revert on Windows, WSL and Linux SSH projects.",
        "Captures bounded file snapshots on the execution target, outside your project. It preserves Git staging and conversation history. Later edits, incomplete capture and overlapping conversations prevent automatic restoration. Requires Python 3 and Git on each target; no configuration-file editing is needed.",
        "", "", "", new("https://pi.dev/docs/latest/extensions"), new("https://pi.dev/docs/latest/extensions"),
        () => (CheckpointSettings.IsEnabled() ? "Enabled · readiness checked per conversation" : "Disabled · no snapshots captured", true), Bundled: true);
    public static ExtensionDefinition Permissions { get; } = new(
        "Permission Modes", "GeorgeDong32", PermissionModesSupport.Version,
        "Control when Pi asks before changing files or running commands. Switch between manual approval, planning, automatic review, and bypass from the conversation input.",
        "Ask prompts for changes and mutating commands. Plan restricts writes to its plan file and limits commands to a read-only allowlist. Auto reviews tool actions using the extension's rules and a separate model. Bypass skips approval prompts. Existing rules and model profiles still apply. These are agent policies, not an operating-system sandbox.",
        PermissionModesSupport.InstallCommand,
        "The command installs the supported third-party package and applies Pi desktop's compatibility repair: omit the unsupported temperature option only for Codex classifier requests. It keeps a backup and does not change approval rules or fail-closed behavior. Reinstalling or updating the extension can remove this repair; run Set up again if the card requests it. Auto uses a separate approval model, defaulting to anthropic/claude-haiku-4-5. Choose a connected model in permission-modes.json in your Pi agent directory (~/.pi/agent by default). Merge these fields into any existing configuration rather than replacing your rules:",
        "{\n  \"classifier\": {\n    \"model\": \"openai-codex/gpt-5.5\",\n    \"failClosed\": true\n  }\n}",
        new("https://github.com/GeorgeDong32/pi-permission-modes#readme"),
        new("https://github.com/GeorgeDong32/pi-permission-modes"),
        () => PermissionModesSupport.GetInstallationState(), Essential: true);

    public static ExtensionDefinition AutomaticTitles { get; } = new(
        "Automatic titles", "patlux", "0.1.1",
        "Find conversations at a glance with short, automatic titles after their first completed run. Titles you rename manually stay yours.",
        "Generates a short title from a bounded excerpt of user and assistant messages. Pi desktop shows the title in the sidebar and conversation header, and preserves manual renames across restarts. Naming makes a separate model request and uses that provider's credentials and usage allowance.",
        AutoSessionNameSupport.InstallCommand,
        "The naming model defaults to openai-codex/gpt-5.4-mini, independently of the chat dropdown. Choose a connected model in ~/.pi/agent/extensions/auto-session-name.json. For example:",
        "{\n  \"provider\": \"openai-codex\",\n  \"model\": \"gpt-5.5\"\n}",
        new("https://github.com/patlux/pi-auto-session-name#readme"),
        new("https://github.com/patlux/pi-auto-session-name"), AutoSessionNameSupport.GetInstallationState, Essential: true);

    public static ExtensionDefinition Mcp { get; } = new(
        "MCP Adapter", "Nico Bailon", McpAdapterSupport.Version,
        "Connect Pi to tools and services such as Obsidian and Jira through MCP servers. Add integrations and inspect their connection status in the MCP panel.",
        "A third-party extension maintained by Nico Bailon, supported by Pi desktop. The optional Skills & MCP panel displays the adapter's read-only runtime status. Server configuration and authentication remain managed by the extension; opening the panel does not connect servers. Older adapter versions need an update for live status.",
        "pi install npm:pi-mcp-adapter@" + McpAdapterSupport.Version,
        "Configure your servers in ~/.pi/agent/mcp.json (or mcp.json in your custom PI_CODING_AGENT_DIR). Add each server's configuration under mcpServers using the upstream documentation, then restart Pi desktop. This empty structure does not connect any servers:",
        "{\n  \"mcpServers\": {}\n}",
        new("https://github.com/nicobailon/pi-mcp-adapter#readme"),
        new("https://github.com/nicobailon/pi-mcp-adapter"),
        () => McpAdapterSupport.GetInstallationState());

    public static ExtensionDefinition Search { get; } = new(
        "Pi Search", "heyhuynhgiabuu", PiSearchSupport.Version,
        "Give Pi access to web search, documentation lookup and page content for answers grounded in current sources. Also provides search for background research.",
        "A third-party extension providing web search, code search, documentation lookup and page fetching. Main conversations use its configured tools. Background research loads only web search, with bounded queries and the app's separate page reader. Installation does not verify provider availability or override permission rules.",
        PiSearchSupport.InstallCommand,
        "Basic search uses Exa's public MCP service without an API key. Optional provider keys and disabled tools can be configured in ~/.pi/pi-search.json following the upstream documentation. Restart Pi desktop after installation; new research workers use the installed supported version. Search queries are sent to the configured provider, whose limits and charges apply.",
        "",
        new("https://github.com/heyhuynhgiabuu/pi-search#readme"),
        new("https://github.com/heyhuynhgiabuu/pi-search"),
        () => PiSearchSupport.GetInstallationState());

    public static ExtensionDefinition Lsp { get; } = new(
        "Language intelligence", "trotsky1997", LspSupport.Version,
        "Help Pi navigate code and check for language errors through language servers. Completed diagnostics can appear in file-change summaries; language servers require separate setup.",
        "A third-party extension maintained by trotsky1997, supported by Pi desktop. The published version supports languages including TypeScript, JavaScript, Svelte, Python, Go and Rust; it does not include C# support. Pi desktop reads structured diagnostic tool results for changed files, separately from lint and tests. It does not run checks automatically or manage language servers.",
        LspSupport.InstallCommand,
        "Run the entire setup command: it installs the extension and pins its protocol dependency to 3.17.5. Newer protocol releases break this extension's imports and prevent Pi from starting. The pin applies only to lsp-pi. Then install and configure the language server for each language you use and restart Pi desktop. Package installation alone does not mean a server is available. Ask Pi to run LSP diagnostics after edits to include the latest check in the summary. Automatic hook messages are not counted as completed checks.",
        "",
        new("https://www.npmjs.com/package/lsp-pi/v/1.0.5"),
        new("https://github.com/trotsky1997/pi-lsp-extension"),
        () => LspSupport.GetInstallationState());

    public static ExtensionDefinition Browser { get; } = new(
        "Pi Browser", "larsderidder", "0.1.0",
        "Let Pi browse pages, interact with forms, capture screenshots and inspect browser errors. Connect an external Chromium browser; this extension does not add an embedded browser panel.",
        "A third-party extension maintained by larsderidder, not developed by Pi desktop. Uses Playwright directly to control a Chromium-based browser through its debugging connection. Pi desktop recommends the extension and checks global installation files only; browser connectivity and embedded WebView2 compatibility are not verified.",
        "pi install git:github.com/larsderidder/pi-browser",
        "Requires Git and npm. This installs the current upstream Git revision; the displayed version is a reference, not a pinned release. Restart Pi desktop after installation. To connect an external Chromium-based browser, start it with a separate user-data directory and --remote-debugging-port=9222, then send /browser connect 9222 in the conversation. Use /browser status to inspect the connection and /browser disconnect to detach. Use separate ports and profiles for independent conversations. Follow the upstream documentation for browser setup. Installing this extension does not add a browser panel or automatically connect a browser.",
        "",
        new("https://github.com/larsderidder/pi-browser#readme"),
        new("https://github.com/larsderidder/pi-browser"),
        () => PiBrowserSupport.GetInstallationState(), RecommendationOnly: true);

    public static IReadOnlyList<ExtensionDefinition> All { get; } = [Checkpoints, Permissions, AutomaticTitles, Search, Mcp, Lsp, Browser];
}
