using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

/// <summary>Previews the saved file and explicitly edits disk without changing Pi's loaded context.</summary>
public sealed class InstructionFileDialog : Controls.ActionContentDialog
{
    private readonly InstructionDocument original;
    private readonly InstructionFileStore store;
    private readonly Grid body = new() { RowSpacing = 10 };
    private readonly TextBox editor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 262144,
        FontSize = 13, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), Visibility = Visibility.Collapsed };
    private readonly ScrollViewer preview;
    private readonly InfoBar error = new() { Severity = InfoBarSeverity.Error, IsClosable = false };
    private bool editing;
    private bool saving;
    public bool Saved { get; private set; }

    public InstructionFileDialog(InstructionItem item, InstructionDocument document, InstructionFileStore store)
    {
        original = document; this.store = store;
        Title = item.Name;
        SecondaryButtonText = "Edit";
        CloseButtonText = "Close";
        DefaultButton = ContentDialogButton.Close;
        Resources["ContentDialogMaxWidth"] = 1000d;
        Resources["ContentDialogMaxHeight"] = 10000d;
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(error);
        var description = new TextBlock { Text = item.Scope + " · Saved file on disk\n" +
            (string.Equals(document.ContentHash, item.File.Hash, StringComparison.OrdinalIgnoreCase) ? "Loaded · matches disk" : "Disk differs · reload required") +
            "\nEdits affect all sessions that load this file. Restart Pi Agent to apply saved changes.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        Grid.SetRow(description, 1); body.Children.Add(description);
        preview = new ScrollViewer { Content = new Controls.MarkdownMessage { Text = document.Text }, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(preview, 2); body.Children.Add(preview);
        editor.Text = document.Text;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(editor, "Instruction file contents");
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        Grid.SetRow(editor, 2); body.Children.Add(editor);
        Content = body;
        Opened += (_, _) => { Resize(); XamlRoot.Changed += OnRootChanged; };
        Closed += (_, _) => { if (XamlRoot is not null) XamlRoot.Changed -= OnRootChanged; };
        Closing += (_, args) => { if (saving) args.Cancel = true; };
        SecondaryButtonClick += OnEdit;
        PrimaryButtonClick += OnSave;
    }

    private void OnEdit(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        editing = !editing;
        editor.Text = original.Text;
        editor.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        preview.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        PrimaryButtonText = editing ? "Save" : "";
        SecondaryButtonText = editing ? "Cancel editing" : "Edit";
        CloseButtonText = editing ? "Cancel" : "Close";
        error.IsOpen = false;
        if (editing) editor.Focus(FocusState.Programmatic);
    }

    private async void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!editing || saving) { args.Cancel = true; return; }
        var deferral = args.GetDeferral();
        saving = true; IsPrimaryButtonEnabled = false; IsSecondaryButtonEnabled = false;
        try { await store.SaveAsync(original, editor.Text); Saved = true; }
        catch (Exception exception)
        {
            args.Cancel = true;
            error.Message = exception is IOException ? exception.Message : "Couldn't save the file. Check write access and try again.";
            error.IsOpen = true;
        }
        finally { saving = false; IsPrimaryButtonEnabled = true; IsSecondaryButtonEnabled = true; deferral.Complete(); }
    }

    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Resize();
    private void Resize()
    {
        body.Width = Math.Max(160, Math.Min(880, XamlRoot.Size.Width - 120));
        body.Height = Math.Max(120, XamlRoot.Size.Height - 150);
    }
}
