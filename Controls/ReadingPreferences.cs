using PiAgentGui.Models.Settings;

namespace PiAgentGui.Controls;

/// <summary>UI-thread presentation values shared by native transcript controls.</summary>
internal static class ReadingPreferences
{
    internal static AppPreferences Current { get; private set; } = new();
    internal static double Body => Current.ConversationTextSize;
    internal static double Code => Current.CodeTextSize;
    internal static double Scale => Body / 15;
    internal static event EventHandler? Changed;
    internal static event EventHandler? TypographyChanged;
    internal static void Apply(AppPreferences preferences)
    {
        var previous = Current;
        Current = preferences;
        if (previous.ConversationTextSize != preferences.ConversationTextSize || previous.CodeTextSize != preferences.CodeTextSize)
            TypographyChanged?.Invoke(null, EventArgs.Empty);
        if (previous.ConversationTextSize != preferences.ConversationTextSize || previous.CodeTextSize != preferences.CodeTextSize
            || previous.ControlEnterToSend != preferences.ControlEnterToSend) Changed?.Invoke(null, EventArgs.Empty);
    }
}
