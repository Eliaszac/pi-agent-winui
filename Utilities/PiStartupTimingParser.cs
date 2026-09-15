using System.Text;
using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>Reads only Pi timing tables, never general stderr or extension paths.</summary>
public sealed class PiStartupTimingParser(Action<string, double> record)
{
    private readonly StringBuilder line = new();
    private string? group;
    private int row;
    public void Append(ReadOnlySpan<char> characters)
    {
        foreach (var character in characters)
        {
            if (character == '\n') { ReadLine(line.ToString().TrimEnd('\r')); line.Clear(); }
            else if (line.Length < 2048) line.Append(character);
        }
    }
    private void ReadLine(string text)
    {
        var handler = Regex.Match(text, @"^PI_GUI_HANDLER (session_start\.[A-Za-z0-9_.-]{1,120}) ([0-9]+)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        if (handler.Success && double.TryParse(handler.Groups[2].Value, out var duration))
        {
            record("pi.handler." + handler.Groups[1].Value, duration);
            return;
        }
        if (text is "--- Startup Timings: main ---" or "--- Startup Timings ---") { group = "main"; row = 0; return; }
        if (text == "--- Startup Timings: extensions ---") { group = "extensions"; row = 0; return; }
        if (group is null) return;
        var match = Regex.Match(text, @"^  (.+): ([0-9]+)ms$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        if (!match.Success) { group = null; return; }
        var label = match.Groups[1].Value;
        // Pi may use absolute paths as extension labels. Retain only their table position.
        if (!Regex.IsMatch(label, @"^[A-Za-z][A-Za-z0-9_. -]{0,80}$")) label = "entry-" + row;
        row++;
        if (double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var milliseconds))
            record("pi.startup." + group + "." + label, milliseconds);
    }
}
