using Microsoft.UI.Xaml.Input;

namespace PiAgentGui.Controls;

public sealed class ActionExpander : Expander
{
    public ActionExpander()
    {
        AddHandler(PointerMovedEvent, new PointerEventHandler(UpdateCursor), true);
        AddHandler(PointerEnteredEvent, new PointerEventHandler(UpdateCursor), true);
    }
    private void UpdateCursor(object sender, PointerRoutedEventArgs args) =>
        ProtectedCursor = InteractionCursor.Resolve(args.OriginalSource as DependencyObject, this);
}
