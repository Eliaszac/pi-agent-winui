namespace PiAgentGui.Utilities;

public static class SidePanelCatalog
{
    public static IReadOnlyList<string> Kinds { get; } = ["source", "terminal", "browser", "files", "capabilities", "research", "docker", "processes"];
    public static string Title(string kind) => kind switch { "browser" => "Browser", "source" => "Source control", "terminal" => "Terminal", "files" => "Files", "capabilities" => "Instructions, skills & MCP", "research" => "Research", "docker" => "Docker", "processes" => "Processes", _ => kind };
    public static string Glyph(string kind) => kind switch { "browser" => "\uE774", "source" => "\uE8A5", "terminal" => "\uE756", "files" => "\uE8B7", "capabilities" => "\uE8F1", "research" => "\uE721", "docker" => "\uE7B8", _ => "\uE9D9" };
}
