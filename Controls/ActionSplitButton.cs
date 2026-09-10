using Microsoft.UI.Input;

namespace PiAgentGui.Controls;

public sealed class ActionSplitButton : SplitButton
{
    public ActionSplitButton()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        IsEnabledChanged += (_, _) => ProtectedCursor = InputSystemCursor.Create(IsEnabled ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
    }
}
