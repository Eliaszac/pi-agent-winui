using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace PiAgentGui.Controls;

/// <summary>Preserves native control behavior with a hand cursor for available actions.</summary>
public class ActionMenuFlyoutItem : MenuFlyoutItem
{
    public ActionMenuFlyoutItem()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        IsEnabledChanged += (_, _) => ProtectedCursor = InputSystemCursor.Create(
            IsEnabled ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        // Apply after the native menu's pointer handling, which can reset its cursor.
        ProtectedCursor = InputSystemCursor.Create(
            IsEnabled ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);
    }
}
