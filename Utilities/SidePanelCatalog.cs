namespace PiAgentGui.Utilities;

public static class SidePanelCatalog
{
    public static IReadOnlyList<string> Kinds { get; } = ["source", "terminal", "files", "capabilities", "research", "processes"];
    public static string Title(string kind) => kind switch { "source" => "Source control", "terminal" => "Terminal", "files" => "Files", "capabilities" => "Instructions, skills & MCP", "research" => "Research", "processes" => "Processes", _ => kind };
    public static string Glyph(string kind) => kind switch { "source" => "\uE8A5", "terminal" => "\uE756", "files" => "\uE8B7", "capabilities" => "\uE8F1", "research" => "\uE721", _ => "\uE9D9" };
}
