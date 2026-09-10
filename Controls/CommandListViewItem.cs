using Microsoft.UI.Input;

namespace PiAgentGui.Controls;

public sealed class CommandListViewItem : ListViewItem
{
    public CommandListViewItem() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
}
