using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace PiAgentGui.Controls;

/// <summary>Determines the cursor for native template children without replacing their templates.</summary>
internal static class InteractionCursor
{
    private static readonly InputSystemCursor Arrow = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    private static readonly InputSystemCursor Hand = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    private static readonly InputSystemCursor Text = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
    internal static InputSystemCursor Resolve(DependencyObject? source, DependencyObject boundary)
    {
        for (var current = source; current is not null && current != boundary; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Control { IsEnabled: false }) return Arrow;
            if (current is TextBox or RichEditBox or RichTextBlock || current is TextBlock { IsTextSelectionEnabled: true })
                return Text;
            if (current is ButtonBase or ComboBox or MenuFlyoutItem)
                return Hand;
        }
        return Arrow;
    }
}
