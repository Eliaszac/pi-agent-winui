using PiAgentGui.Controls;

namespace PiAgentGui.Views;

public sealed partial class LegalView : UserControl
{
    public event EventHandler? BackRequested;
    private int requestId;
    public LegalView()
    {
        InitializeComponent();
        DocumentPicker.Items.Add(new ComboBoxItem { Content = "Terms of use", Tag = "TERMS.md" });
        DocumentPicker.Items.Add(new ComboBoxItem { Content = "Privacy & data", Tag = "PRIVACY.md" });
        DocumentPicker.Items.Add(new ComboBoxItem { Content = "MIT license", Tag = "LICENSE.txt" });
        DocumentPicker.Items.Add(new ComboBoxItem { Content = "Third-party notices", Tag = "THIRD-PARTY-NOTICES.md" });
        var directory = Path.Combine(AppContext.BaseDirectory, "Legal", "ThirdParty");
        if (Directory.Exists(directory))
            foreach (var file in Directory.EnumerateFiles(directory).Order())
                DocumentPicker.Items.Add(new ComboBoxItem { Content = Path.GetFileNameWithoutExtension(file), Tag = Path.Combine("ThirdParty", Path.GetFileName(file)) });
        DocumentPicker.SelectedIndex = 0;
    }
    private void OnBack(object sender, RoutedEventArgs args) => BackRequested?.Invoke(this, EventArgs.Empty);
    private async void OnDocumentChanged(object sender, SelectionChangedEventArgs args)
    {
        if (DocumentPicker.SelectedItem is not ComboBoxItem { Tag: string path }) return;
        var request = ++requestId;
        try
        {
            var text = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Legal", path));
            if (request != requestId) return;
            if (path.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
            {
                var document = new RichEditBox { IsReadOnly = true, BorderThickness = new Thickness(0), TextWrapping = TextWrapping.Wrap };
                document.Document.SetText(Microsoft.UI.Text.TextSetOptions.FormatRtf, text);
                DocumentContent.Content = document;
            }
            else DocumentContent.Content = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? MarkdownRenderer.Render(Markdig.Markdown.Parse(text))
                : new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            DocumentScroll.ChangeView(null, 0, null, true);
        }
        catch (Exception)
        {
            if (request == requestId) DocumentContent.Content = new TextBlock { Text = "This document could not be read. The legal documents are also available in the source repository.", TextWrapping = TextWrapping.Wrap };
        }
    }
}
