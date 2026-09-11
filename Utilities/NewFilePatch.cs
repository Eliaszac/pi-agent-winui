using System.Text;

namespace PiAgentGui.Utilities;

public static class NewFilePatch
{
    public static string Create(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null) lines.Add(line);
        if (lines.Count == 0) return "";
        var patch = new StringBuilder($"@@ -0,0 +1,{lines.Count} @@\n");
        foreach (var value in lines) patch.Append('+').Append(value).Append('\n');
        if (!text.EndsWith('\n')) patch.Append("\\ No newline at end of file\n");
        return patch.ToString();
    }
}
