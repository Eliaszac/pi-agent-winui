using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Separates the permission extension's inline preview without changing its response values.</summary>
public static class ExtensionPromptPresentation
{
    public static (string Heading, string Preview) Split(ExtensionPrompt prompt)
    {
        if (prompt.Method == "select" && prompt.Title.StartsWith("Allow ", StringComparison.Ordinal)
            && prompt.Options.Contains("Allow") && prompt.Options.Contains("Block"))
        {
            var separator = prompt.Title.IndexOf("? ", StringComparison.Ordinal);
            if (separator >= 0)
                return (prompt.Title[..(separator + 1)], prompt.Title[(separator + 2)..]);
        }
        return (prompt.Title, "");
    }
}
