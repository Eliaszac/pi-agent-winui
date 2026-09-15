using PiAgentGui.Models.Settings;

namespace PiAgentGui.Controls;

internal static class TerminalPreferences
{
    internal static AppPreferences Current { get; private set; } = new();
    internal static event EventHandler? Changed;
    internal static void Apply(AppPreferences preferences)
    {
        var previous = Current;
        Current = preferences;
        if (previous.TerminalTextSize != preferences.TerminalTextSize || previous.TerminalScrollback != preferences.TerminalScrollback)
            Changed?.Invoke(null, EventArgs.Empty);
    }
}
