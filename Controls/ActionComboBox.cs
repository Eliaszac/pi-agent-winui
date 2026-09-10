using Microsoft.UI.Input;

namespace PiAgentGui.Controls;

public sealed class ActionComboBox : ComboBox
{
    public ActionComboBox()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        IsEnabledChanged += (_, _) => ProtectedCursor = InputSystemCursor.Create(
            IsEnabled ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
    }

    protected override DependencyObject GetContainerForItemOverride() => new ActionComboBoxItem();
}
