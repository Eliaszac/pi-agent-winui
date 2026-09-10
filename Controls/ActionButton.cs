using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace PiAgentGui.Controls;

/// <summary>Preserves native control behavior with a hand cursor for available actions.</summary>
public class ActionButton : Button
{
    public ActionButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        IsEnabledChanged += (_, _) => ProtectedCursor = InputSystemCursor.Create(
            IsEnabled ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
    }
}
