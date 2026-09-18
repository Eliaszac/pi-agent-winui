using Microsoft.UI.Xaml.Input;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.Utilities;
using Windows.System;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private readonly FileReferenceSearch fileSearch = new();
    private CancellationTokenSource? fileSearchLifetime;
    private FileReferenceToken? fileToken;
    private string? dismissedFileToken;
    private string referenceKind = "files";
    private bool reviewingScript;
    public Func<string, IReadOnlyList<Models.Projects.ProjectScript>>? SearchScripts { get; set; }
    public Func<Models.Projects.ProjectScript, Task<bool>>? ReviewScript { get; set; }
    private static string ReferenceSourceName(string kind) => kind switch
    {
        "prs" => "Pull requests", "issues" => "Issues", "scripts" => "Run script", _ => "Files"
    };
    private void OnReferenceKindChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: string kind }) return;
        referenceKind = kind;
        ReferenceSourceLabel.Text = ReferenceSourceName(kind);
        if (fileToken is { Kind: not null } scoped)
        {
            var caret = Composer.SelectionStart;
            var colon = Composer.Text.IndexOf(':', scoped.Start);
            var prefix = "@" + kind + ":";
            Composer.Text = Composer.Text.Remove(scoped.Start, colon - scoped.Start + 1).Insert(scoped.Start, prefix);
            Composer.Select(caret + prefix.Length - (colon - scoped.Start + 1), 0);
        }
        fileToken = null; dismissedFileToken = null; UpdateFileReferences();
        Composer.Focus(FocusState.Programmatic);
    }

    private async void UpdateFileReferences()
    {
        if (FileReferencePanel is null) return;
        if (composing) return;
        var nextToken = FileReferenceToken.Find(Composer.Text, Composer.SelectionStart, Composer.SelectionLength);
        if (nextToken?.Kind is { } kind)
        {
            referenceKind = kind;
            ReferenceSourceLabel.Text = ReferenceSourceName(kind);
        }
        if (nextToken is null && Composer.SelectionLength == 0)
        {
            var start = Composer.SelectionStart;
            while (start > 0 && !char.IsWhiteSpace(Composer.Text[start - 1])) start--;
            var word = Composer.Text[start..Composer.SelectionStart];
            if (Models.GitHub.GitHubReference.FromUrl(word) is not null) nextToken = new(start, word.Length, word);
        }
        if (nextToken == fileToken && (nextToken is null || FileReferencePanel.Visibility == Visibility.Visible)) return;
        fileSearchLifetime?.Cancel();
        fileToken = nextToken;
        var token = fileToken;
        var owner = ViewModel;
        if (token is null || owner is not { IsReady: true } || composing)
        {
            if (FileReferencePanel.Visibility != Visibility.Collapsed) FileReferencePanel.Visibility = Visibility.Collapsed;
            if (token is null) dismissedFileToken = null;
            return;
        }
        if (dismissedFileToken == $"{token.Start}:{token.Query}") return;
        FileReferencePanel.Visibility = Visibility.Visible;
        BrowseReferenceButton.Visibility = referenceKind == "files" ? Visibility.Visible : Visibility.Collapsed;
        FileReferenceList.ItemsSource = null;
        ReferenceHint.Text = referenceKind switch
        {
            "files" => "Project files", "scripts" => "Finding saved project scripts…",
            _ => "Searching this project's GitHub repository…"
        };
        var request = new CancellationTokenSource();
        fileSearchLifetime = request;
        try
        {
            var source = referenceKind;
            await Task.Delay(source is "files" or "scripts" ? 120 : 400, request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(owner, ViewModel)) return;
            if (source == "scripts" && Models.GitHub.GitHubReference.FromUrl(token.Query) is null)
            {
                var scripts = SearchScripts?.Invoke(token.Query) ?? throw new InvalidOperationException("Project scripts are unavailable.");
                FileReferenceList.ItemsSource = scripts;
                FileReferenceList.SelectedIndex = scripts.Count > 0 ? 0 : -1;
                ReferenceHint.Text = scripts.Count == 0
                    ? "No matching scripts. Add or refresh scripts from Run scripts."
                    : "Action, not context · Select to review, then choose Run · Up to 20 matches";
                return;
            }
            if (source != "files" || Models.GitHub.GitHubReference.FromUrl(token.Query) is not null)
            {
                if (owner.GitHub is null) throw new IOException("Connect GitHub from Settings → Integrations.");
                var repository = await owner.GetGitHubRepositoryAsync(request.Token);
                var references = await owner.GitHub.SearchReferencesAsync(token.Query, source == "prs", repository, request.Token);
                if (request.IsCancellationRequested || !ReferenceEquals(owner, ViewModel)) return;
                FileReferenceList.ItemsSource = references;
                FileReferenceList.SelectedIndex = references.Count > 0 ? 0 : -1;
                ReferenceHint.Text = repository + (references.Count == 0 ? " · No matching PRs or issues." : " · Up to 20 matches · All states");
                return;
            }
            var matches = owner.Target is { IsLocal: false } target
                ? await new Services.Files.TargetFileReader(target, new Services.Projects.TargetCommandRunner()).FindAsync(token.Query, request.Token)
                : await fileSearch.FindAsync(owner.WorkingDirectory, token.Query, request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(owner, ViewModel)) return;
            FileReferenceList.ItemsSource = matches;
            FileReferenceList.SelectedIndex = matches.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!request.IsCancellationRequested && ReferenceEquals(owner, ViewModel))
                ReferenceHint.Text = error switch
                {
                    IOException or UnauthorizedAccessException or InvalidOperationException => error.Message,
                    System.ComponentModel.Win32Exception => "Couldn't start Git or the execution target. Check its installation and connection.",
                    System.Net.Http.HttpRequestException => "Couldn't reach GitHub. Check your connection and try again.",
                    _ => "Reference lookup failed (" + error.GetType().Name + "). Try again."
                };
        }
        finally { if (ReferenceEquals(fileSearchLifetime, request)) fileSearchLifetime = null; request.Dispose(); }
    }

    private void DismissFileReferences()
    {
        fileSearchLifetime?.Cancel();
        if (fileToken is { } token) dismissedFileToken = $"{token.Start}:{token.Query}";
        if (FileReferencePanel is not null) FileReferencePanel.Visibility = Visibility.Collapsed;
    }

    private bool HandleFileReferenceKey(KeyRoutedEventArgs args)
    {
        if (FileReferencePanel.Visibility != Visibility.Visible) return false;
        if (args.Key is VirtualKey.Up or VirtualKey.Down)
        {
            var count = FileReferenceList.Items.Count;
            if (count > 0)
            {
                FileReferenceList.SelectedIndex = (FileReferenceList.SelectedIndex + (args.Key == VirtualKey.Down ? 1 : -1) + count) % count;
                FileReferenceList.ScrollIntoView(FileReferenceList.SelectedItem);
            }
        }
        else if (args.Key == VirtualKey.Escape) DismissFileReferences();
        else if (args.Key == VirtualKey.Tab) AcceptFileReference();
        else return false;
        args.Handled = true;
        return true;
    }

    private void OnFileReferenceClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is Models.Projects.ProjectScript script) ReviewScriptSelection(script);
        else if (args.ClickedItem is FileReference file) InsertFileReference(file.Path);
        else if (args.ClickedItem is Models.GitHub.GitHubReference reference) InsertGitHubReference(reference);
    }
    private void OnFileReferenceKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter) { AcceptFileReference(); args.Handled = true; }
        else HandleFileReferenceKey(args);
    }
    private void AcceptFileReference()
    {
        if (FileReferenceList.SelectedItem is Models.Projects.ProjectScript script) ReviewScriptSelection(script);
        else if (FileReferenceList.SelectedItem is FileReference file) InsertFileReference(file.Path);
        else if (FileReferenceList.SelectedItem is Models.GitHub.GitHubReference reference) InsertGitHubReference(reference);
    }
    private async void ReviewScriptSelection(Models.Projects.ProjectScript script)
    {
        if (reviewingScript || fileToken is not { } token || ViewModel is not { } owner || ReviewScript is null) return;
        reviewingScript = true;
        var draft = Composer.Text;
        DismissFileReferences();
        try
        {
            var ran = await ReviewScript(script);
            if (!ReferenceEquals(owner, ViewModel)) return;
            if (ran && Composer.Text == draft)
            {
                Composer.Text = draft.Remove(token.Start, token.Length);
                Composer.Select(token.Start, 0);
            }
            Composer.Focus(FocusState.Programmatic);
        }
        catch (Exception error) { owner.ReportAttachmentError(error.Message); }
        finally { reviewingScript = false; }
    }

    private void InsertGitHubReference(Models.GitHub.GitHubReference reference)
    {
        if (fileToken is not { } token || ViewModel is not { } owner) return;
        try
        {
            owner.AttachGitHub(reference);
            DismissFileReferences();
            Composer.Text = Composer.Text.Remove(token.Start, token.Length);
            Composer.Select(token.Start, 0);
            Composer.Focus(FocusState.Programmatic);
        }
        catch (Exception error) { owner.ReportAttachmentError(error.Message); }
    }
    private void OnRemoveGitHubReference(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: Models.GitHub.GitHubReference reference }) ViewModel?.RemoveGitHub(reference);
    }
    private void InsertFileReference(string path, int? selectionStart = null, int selectionLength = 0)
    {
        var token = fileToken;
        if (token is null && selectionStart is null) return;
        var start = token?.Start ?? selectionStart!.Value;
        var length = token?.Length ?? selectionLength;
        var text = Composer.Text;
        DismissFileReferences();
        var label = ViewModel!.FileReferences.Add(path);
        var reference = (start > 0 && !char.IsWhiteSpace(text[start - 1]) ? " " : "") + "@" + label + " ";
        Composer.Text = text.Remove(start, length).Insert(start, reference);
        Composer.Select(start + reference.Length, 0);
        Composer.Focus(FocusState.Programmatic);
    }
    private async void OnBrowseReference(object sender, RoutedEventArgs args)
    {
        var owner = ViewModel;
        if (owner?.IsRemoteTarget == true) { owner.ReportAttachmentError("Use @ to search files on the execution target. The Windows file picker selects local files only."); return; }
        var text = Composer.Text;
        var token = fileToken;
        var selectionStart = Composer.SelectionStart;
        var selectionLength = Composer.SelectionLength;
        try
        {
            var path = await new FileReferencePicker().PickAsync(XamlRoot);
            if (path is not null && ReferenceEquals(owner, ViewModel) && text == Composer.Text && token == fileToken)
                InsertFileReference(path, selectionStart, selectionLength);
        }
        catch (Exception) { owner?.ReportAttachmentError("Couldn't open the file picker. Try again."); }
    }
}
