namespace PiAgentGui.Views;

public sealed class ErrorDetailsDialog : Controls.ActionContentDialog
{
    public ErrorDetailsDialog(string title, string help, string diagnostics)
    {
        Title = title; CloseButtonText = "Close";
        var panel = new StackPanel { Spacing = 12, MaxWidth = 620 };
        panel.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new Controls.CodeBlockView("Diagnostics", diagnostics));
        Content = new ScrollViewer { Content = panel, MaxHeight = 480, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
