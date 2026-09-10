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

    public static IReadOnlyList<ExtensionDefinition> All { get; } = [Permissions, AutomaticTitles];
}
