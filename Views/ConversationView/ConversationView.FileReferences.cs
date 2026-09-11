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

    private async void UpdateFileReferences()
    {
        if (FileReferencePanel is null) return;
        if (composing) return;
        var nextToken = FileReferenceToken.Find(Composer.Text, Composer.SelectionStart, Composer.SelectionLength);
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
        FileReferenceList.ItemsSource = null;
        var request = new CancellationTokenSource();
        fileSearchLifetime = request;
        try
        {
            await Task.Delay(120, request.Token);
            var matches = await fileSearch.FindAsync(owner.WorkingDirectory, token.Query, request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(owner, ViewModel)) return;
            FileReferenceList.ItemsSource = matches;
            FileReferenceList.SelectedIndex = matches.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (ReferenceEquals(owner, ViewModel)) owner.ReportAttachmentError("Couldn't search project files. Use Browse to choose a file."); }
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
        if (args.ClickedItem is FileReference file) InsertFileReference(file.Path);
    }
    private void OnFileReferenceKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter) { AcceptFileReference(); args.Handled = true; }
        else HandleFileReferenceKey(args);
    }
    private void AcceptFileReference()
    {
        if (FileReferenceList.SelectedItem is FileReference file) InsertFileReference(file.Path);
    }
    private void InsertFileReference(string path)
    {
        var token = fileToken;
        if (token is null) return;
        var text = Composer.Text;
        DismissFileReferences();
        var label = ViewModel!.FileReferences.Add(path);
        var reference = "@" + label + " ";
        Composer.Text = text.Remove(token.Start, token.Length).Insert(token.Start, reference);
        Composer.Select(token.Start + reference.Length, 0);
        Composer.Focus(FocusState.Programmatic);
    }
    private async void OnBrowseReference(object sender, RoutedEventArgs args)
    {
        var owner = ViewModel;
        var text = Composer.Text;
        var token = fileToken;
        try
        {
            var path = await new FileReferencePicker().PickAsync(XamlRoot);
            if (path is not null && ReferenceEquals(owner, ViewModel) && text == Composer.Text && token == fileToken)
                InsertFileReference(path);
        }
        catch (Exception) { owner?.ReportAttachmentError("Couldn't open the file picker. Try again."); }
    }
}
