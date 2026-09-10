namespace PiAgentGui.Controls;

public sealed class CommandListView : ListView
{
    protected override DependencyObject GetContainerForItemOverride() => new CommandListViewItem();
}
