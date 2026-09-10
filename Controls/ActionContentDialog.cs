using Microsoft.UI.Xaml.Input;

namespace PiAgentGui.Controls;

public class ActionContentDialog : ContentDialog
{
    public ActionContentDialog()
    {
        AddHandler(PointerMovedEvent, new PointerEventHandler(UpdateCursor), true);
        AddHandler(PointerEnteredEvent, new PointerEventHandler(UpdateCursor), true);
    }
    private void UpdateCursor(object sender, PointerRoutedEventArgs args) =>
        ProtectedCursor = InteractionCursor.Resolve(args.OriginalSource as DependencyObject, this);
}
