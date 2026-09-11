namespace PiAgentGui.Views;

/// <summary>A wider skill reader using the full-height diff dialog's viewport margins.</summary>
public sealed class SkillPreviewDialog : Controls.ActionContentDialog
{
    private readonly ScrollViewer body;

    public SkillPreviewDialog(string name, string markdown)
    {
        Title = name;
        CloseButtonText = "Close";
        DefaultButton = ContentDialogButton.Close;
        Resources["ContentDialogMaxWidth"] = 1000d;
        Resources["ContentDialogMaxHeight"] = 10000d;
        body = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new Controls.MarkdownMessage { Text = markdown }
        };
        Content = body;
        Opened += (_, _) => { Resize(); XamlRoot.Changed += OnRootChanged; };
        Closed += (_, _) => { if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged; };
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();

    private void Resize()
    {
        body.Width = Math.Max(160, Math.Min(880, XamlRoot.Size.Width - 120));
        body.Height = Math.Max(120, XamlRoot.Size.Height - 150);
    }
}
