namespace PiAgentGui.Utilities;

/// <summary>Recognized fence languages and safe generated filename extensions.</summary>
public static class SnippetLanguage
{
    public static string Normalize(string label) => label.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() switch
    {
        "ps" or "ps1" or "pwsh" or "powershell" => "powershell",
        "sh" or "shell" or "bash" => "bash",
        "py" or "python" or "python3" => "python",
        var other => other ?? "text"
    };
    public static bool CanRun(string label) => Normalize(label) is "powershell" or "bash" or "python";
    public static string Extension(string label) => Normalize(label) switch
    {
        "powershell" => ".ps1", "bash" => ".sh", "python" => ".py",
        "javascript" or "js" => ".js", "typescript" or "ts" => ".ts", "tsx" => ".tsx", "jsx" => ".jsx",
        "csharp" or "cs" => ".cs", "json" => ".json", "yaml" or "yml" => ".yaml",
        "html" => ".html", "css" => ".css", "sql" => ".sql", "xml" => ".xml",
        "markdown" or "md" => ".md", "rust" or "rs" => ".rs", "go" => ".go",
        "java" => ".java", "c" => ".c", "cpp" or "c++" => ".cpp", _ => ".txt"
    };
    public static string FileName(string label, int index) => "snippet" + (index == 1 ? "" : "-" + index) + Extension(label);
}
