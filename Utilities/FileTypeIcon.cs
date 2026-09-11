namespace PiAgentGui.Utilities;

public static class FileTypeIcon
{
    public static string Name(string path, bool folder, bool expanded) => folder ? (expanded ? "NeutralFolderOpened" : "NeutralFolderClosed") : Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" => "material-csharp", ".cpp" or ".c" or ".h" or ".hpp" => "material-cpp",
        ".ts" => "material-typescript", ".tsx" or ".jsx" => "material-react",
        ".js" or ".mjs" or ".cjs" => "material-javascript",
        ".json" => "material-json", ".xml" or ".xaml" or ".csproj" or ".props" => "material-xml",
        ".html" or ".htm" => "material-html", ".svelte" => "material-svelte", ".vue" => "material-vue",
        ".css" => "material-css", ".scss" or ".sass" => "material-sass",
        ".py" => "material-python", ".java" => "material-java", ".md" or ".mdx" => "material-markdown",
        ".yaml" or ".yml" => "material-yaml", ".ps1" or ".psm1" => "material-powershell",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".webp" or ".svg" => "material-image",
        _ => "TextFile"
    };
}
