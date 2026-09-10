using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Combines native actions with Pi-discovered commands without exposing source paths.</summary>
public static class ComposerCommandCatalog
{
    public static IReadOnlyList<ComposerCommand> Create(JsonElement commands)
    {
        var result = new List<ComposerCommand>
        {
            new("session", "View model, token usage, cost, and context usage", "app", "details"),
            new("compact", "Compact context, with optional instructions", "app", "compact"),
            new("export", "Save this conversation as HTML", "app", "export"),
            new("model", "Choose this conversation's model", "app", "model"),
            new("thinking", "Choose this conversation's reasoning effort", "app", "thinking"),
            new("copy", "Copy the latest completed response", "app", "copy"),
            new("new", "Create a conversation in this project", "app", "new"),
            new("name", "Rename this conversation in the header", "app", "name"),
            new("extensions", "Open installed and supported extensions", "app", "extensions")
        };
        if (commands.ValueKind == JsonValueKind.Array)
            foreach (var item in commands.EnumerateArray())
            {
                var name = PiJson.Text(item, "name");
                var source = PiJson.Text(item, "source");
                if (name.Length == 0 || name.Any(char.IsWhiteSpace) || name.Contains('/') || result.Any(command => command.Name == name)) continue;
                if (source is not ("extension" or "skill" or "prompt")) continue;
                result.Add(new(name, name == "auto-name" ? "Regenerate the conversation title" : PiJson.Text(item, "description"), source));
            }
        return result;
    }

    public static bool IsUnsupportedNativeCommand(string name) => name is "login" or "logout" or "llama" or "scoped-models"
        or "settings" or "resume" or "tree" or "trust" or "fork" or "clone" or "import" or "share" or "reload" or "hotkeys" or "changelog" or "quit";

    public static IReadOnlyList<ComposerCommand> Filter(IReadOnlyList<ComposerCommand> commands, string query) => commands
        .Where(command => command.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || command.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(command => command.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        .ThenBy(command => command.Source != "app").ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase).ToArray();
}
