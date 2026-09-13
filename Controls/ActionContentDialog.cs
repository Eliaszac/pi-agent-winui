using Microsoft.UI.Xaml.Input;

namespace PiAgentGui.Controls;

public class ActionContentDialog : ContentDialog
{
    public ActionContentDialog()
    {
        Opened += (_, _) => RequestedTheme = XamlRoot?.Content is FrameworkElement root ? root.RequestedTheme : ElementTheme.Default;
        AddHandler(PointerMovedEvent, new PointerEventHandler(UpdateCursor), true);
        AddHandler(PointerEnteredEvent, new PointerEventHandler(UpdateCursor), true);
    }
    private void UpdateCursor(object sender, PointerRoutedEventArgs args) =>
        ProtectedCursor = InteractionCursor.Resolve(args.OriginalSource as DependencyObject, this);
}
