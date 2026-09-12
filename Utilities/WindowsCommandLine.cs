using System.Text;

namespace PiAgentGui.Utilities;

/// <summary>Encodes one argv value using the Windows C runtime command-line rules.</summary>
public static class WindowsCommandLine
{
    public static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
