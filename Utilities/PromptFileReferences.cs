using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>Keeps draft labels separate from paths sent to Pi.</summary>
public sealed class PromptFileReferences
{
    private readonly Dictionary<string, string> paths = new(StringComparer.Ordinal);
    private static readonly Regex References = new("@\"([^\"]+)\"", RegexOptions.CultureInvariant);
    public string Add(string path)
    {
        foreach (var item in paths)
            if (string.Equals(item.Value, path, StringComparison.OrdinalIgnoreCase)) return item.Key;
        var name = FileName(path);
        var label = name;
        for (var suffix = 2; paths.ContainsKey(label); suffix++) label = $"{name} ({suffix})";
        paths.Add(label, path);
        return label;
    }
    public string Expand(string text)
    {
        if (paths.Count == 0) return text;
        var labels = string.Join("|", paths.Keys.OrderByDescending(label => label.Length).Select(Regex.Escape));
        return Regex.Replace(text, "(?<!\\S)@(?:\"(?<quoted>" + labels + ")\"|(?<plain>" + labels + ")(?=$|\\s|[.,;!?]))", match =>
        {
            var label = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
            return FileReferenceToken.Quote(paths[label]).TrimEnd();
        });
    }
    public static string Display(string text) => References.Replace(text, match => "@" + FileName(match.Groups[1].Value));
    public string Restore(string text) => References.Replace(text, match => "@" + Add(match.Groups[1].Value));
    private static string FileName(string path) => path.Replace('\\', '/').Split('/')[^1];
}
