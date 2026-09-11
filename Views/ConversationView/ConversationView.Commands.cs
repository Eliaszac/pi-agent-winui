using Microsoft.UI.Xaml.Input;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Dialogs;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private async void OnRestoreSteering(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: PendingPrompt prompt } && ViewModel is { } owner)
            await owner.RestoreRecoveredCommand.ExecuteAsync(prompt);
    }
    public event Action<string, string>? ShellCommandRequested;
    private IReadOnlyList<ComposerCommand> commandCatalog = ComposerCommandCatalog.Create(default);
    private ConversationViewModel? commandOwner;
    private bool loadingCommands;
    private readonly HashSet<ConversationViewModel> executingCommands = [];
    private bool pickerUpdateQueued;
    private string? dismissedToken;
    private SlashCommandToken? commandToken;

    private void ResetCommands()
    {
        DismissFileReferences();
        fileToken = null;
        dismissedFileToken = null;
        commandOwner = null;
        commandCatalog = ComposerCommandCatalog.Create(default);
        dismissedToken = null;
        commandToken = null;
        CommandPanel.Visibility = Visibility.Collapsed;
        CommandNotice.IsOpen = false;
        RefreshCommandSurface();
    }

    private void OnComposerTextChanged(object sender, TextChangedEventArgs args) => QueuePickerUpdate();
    private void OnComposerSelectionChanged(object sender, RoutedEventArgs args) => QueuePickerUpdate();
    private void QueuePickerUpdate()
    {
        if (pickerUpdateQueued) return;
        pickerUpdateQueued = true;
        DispatcherQueue.TryEnqueue(() => { pickerUpdateQueued = false; UpdatePicker(); UpdateFileReferences(); });
    }

    private void UpdatePicker()
    {
        if (CommandPanel is null || composing || (ViewModel is { } current && executingCommands.Contains(current))) return;
        commandToken = SlashCommandToken.Find(Composer.Text, Composer.SelectionStart, Composer.SelectionLength);
        if (commandToken is null || ViewModel is not { IsReady: true })
        {
            if (CommandPanel.Visibility == Visibility.Visible) commandOwner = null;
            CommandPanel.Visibility = Visibility.Collapsed;
            RefreshCommandSurface();
            if (commandToken is null) dismissedToken = null;
            return;
        }
        if (dismissedToken == $"{commandToken.Start}:{commandToken.Query}") return;
        var matches = ComposerCommandCatalog.Filter(ViewModel.IsRunning ? commandCatalog.Where(item => item.Action == "steer").ToArray() : commandCatalog, commandToken.Query);
        var selected = CommandList.SelectedItem as ComposerCommand;
        CommandList.ItemsSource = matches;
        CommandList.SelectedIndex = Math.Max(0, matches.ToList().FindIndex(item => item == selected));
        CommandHint.Text = matches.Count == 0 ? "No matching commands" : loadingCommands ? "Loading Pi commands…" : "Commands";
        CommandPanel.Visibility = Visibility.Visible;
        RefreshCommandSurface();
        if (!ReferenceEquals(commandOwner, ViewModel) && !loadingCommands && ViewModel.CanUseCommands) LoadCommands(ViewModel);
    }

    private async void LoadCommands(ConversationViewModel owner)
    {
        loadingCommands = true;
        try
        {
            var data = await owner.RunOperationAsync(ConversationOperation.Commands);
            if (!ReferenceEquals(ViewModel, owner)) return;
            commandCatalog = ComposerCommandCatalog.Create(PiJson.Field(data, "commands"));
            commandOwner = owner;
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(ViewModel, owner))
            {
                commandOwner = owner;
                ShowCommandNotice("Couldn't load Pi commands. Reopen the picker to retry. " + exception.Message, true);
            }
        }
        finally { loadingCommands = false; UpdatePicker(); }
    }

    private void DismissPicker()
    {
        if (commandToken is { } token) dismissedToken = $"{token.Start}:{token.Query}";
        CommandPanel.Visibility = Visibility.Collapsed;
        RefreshCommandSurface();
        commandOwner = null; // Refresh discovery on the next opening, including resource reloads.
    }

    private void OnComposerPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!composing && HandleFileReferenceKey(args)) return;
        if (composing || CommandPanel.Visibility != Visibility.Visible) return;
        if (args.Key is VirtualKey.Up or VirtualKey.Down)
        {
            args.Handled = true;
            if (CommandList.Items.Count == 0) return;
            CommandList.SelectedIndex = (CommandList.SelectedIndex + (args.Key == VirtualKey.Down ? 1 : -1) + CommandList.Items.Count) % CommandList.Items.Count;
            CommandList.ScrollIntoView(CommandList.SelectedItem);
        }
        else if (args.Key == VirtualKey.Tab) { args.Handled = true; AcceptCommand(); }
        else if (args.Key == VirtualKey.Escape) { args.Handled = true; DismissPicker(); }
    }

    private void OnCommandListKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Enter or VirtualKey.Tab) { args.Handled = true; AcceptCommand(); }
        else if (args.Key == VirtualKey.Escape) { args.Handled = true; DismissPicker(); Composer.Focus(FocusState.Programmatic); }
    }

    private void OnCommandClicked(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is ComposerCommand command) AcceptCommand(command);
    }

    private async void AcceptCommand(ComposerCommand? command = null)
    {
        command ??= CommandList.SelectedItem as ComposerCommand;
        if (command is null || commandToken is null || ViewModel is not { IsReady: true } owner) return;
        if (command.Action == "steer")
        {
            owner.Draft = "/steer " + commandToken.RemoveFrom(Composer.Text);
            DismissPicker();
            Composer.Focus(FocusState.Programmatic);
            Composer.Select(owner.Draft.Length, 0);
            return;
        }
        if (!owner.CanUseCommands) return;
        var text = Composer.Text;
        var token = commandToken;
        DismissPicker();
        await ExecuteComposerCommandAsync(owner, command, text, token, "");
    }

    private async Task<bool> HandleTypedCommandAsync(string text)
    {
        if (ViewModel is not { } owner || !text.StartsWith('/')) return false;
        var end = text.IndexOfAny([' ', '\r', '\n', '\t']);
        if (end < 0) end = text.Length;
        var name = text[1..end];
        var command = commandCatalog.FirstOrDefault(item => item.Name == name);
        if (command is null)
        {
            if (!ComposerCommandCatalog.IsUnsupportedNativeCommand(name)) return false;
            ShowCommandNotice("/" + name + " is a Pi terminal command and isn't available in this app yet.", true);
            return true;
        }
        if (command.Source is "skill" or "prompt") return false;
        DismissPicker();
        await ExecuteComposerCommandAsync(owner, command, text, new(0, text.Length, name), text[end..].Trim());
        return true;
    }

    private async Task ExecuteComposerCommandAsync(ConversationViewModel owner, ComposerCommand command, string original, SlashCommandToken token, string argument)
    {
        if (!executingCommands.Add(owner)) return;
        try
        {
            if (command.Source is "skill" or "prompt")
            {
                owner.Draft = command.Label + " " + token.RemoveFrom(original);
                Composer.Select(command.Label.Length + 1, 0);
                return;
            }
            switch (command.Action)
            {
                case "new": case "name": case "extensions": ShellCommandRequested?.Invoke(command.Action, argument); break;
                case "fork": case "clone": await owner.RequestDuplicateAsync(command.Action == "fork"); break;
                case "details":
                    var data = await owner.RunOperationAsync(ConversationOperation.Details);
                    if (!ReferenceEquals(ViewModel, owner)) return;
                    await ShowCommandDialogAsync("Session details", new TextBlock { Text = SessionDetailsFormatter.Format(data, owner.SelectedModel), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
                    break;
                case "compact":
                    var instructions = new TextBox { Text = argument, PlaceholderText = "Optional instructions for the summary", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, MaxHeight = 180 };
                    if (!await ConfirmCommandAsync("Compact context", "Pi will summarize older context. Your saved conversation remains on disk.", instructions, "Compact")) return;
                    ShowCommandNotice("Compacting context…");
                    await owner.RunOperationAsync(ConversationOperation.Compact, instructions.Text);
                    if (ReferenceEquals(ViewModel, owner)) ShowCommandNotice("Context compacted.");
                    break;
                case "export":
                    var path = await new ConversationExportPicker().PickAsync(XamlRoot);
                    if (path is null) return;
                    await owner.RunOperationAsync(ConversationOperation.ExportHtml, path);
                    if (ReferenceEquals(ViewModel, owner)) ShowCommandNotice("Conversation exported as HTML.");
                    break;
                case "model": ModelSelector.Focus(FocusState.Programmatic); ModelSelector.IsDropDownOpen = true; break;
                case "thinking": ThinkingSelector.Focus(FocusState.Programmatic); ThinkingSelector.IsDropDownOpen = true; break;
                case "copy":
                    var response = owner.Entries.LastOrDefault(entry => entry.CanCopyResponse)?.Text;
                    if (string.IsNullOrEmpty(response)) throw new InvalidOperationException("There isn't a completed response to copy yet.");
                    var clipboard = new DataPackage(); clipboard.SetText(response); Clipboard.SetContent(clipboard);
                    ShowCommandNotice("Response copied.");
                    break;
                default:
                    var input = new TextBox { Text = argument, PlaceholderText = "Optional arguments", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 180 };
                    if (command.Name != "auto-name" && !await ConfirmCommandAsync(command.Label, command.Description, input, "Run")) return;
                    // Close our dialog first so Pi's native approval/select/input requests remain interactive.
                    await owner.SendExtensionCommandAsync(command.Label + (string.IsNullOrWhiteSpace(input.Text) ? "" : " " + input.Text.Trim()));
                    break;
            }
            if (owner.Draft == original) owner.Draft = token.RemoveFrom(original);
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(ViewModel, owner)) ShowCommandNotice(exception.Message, true);
        }
        finally
        {
            executingCommands.Remove(owner);
            if (ReferenceEquals(ViewModel, owner) && command.Action is not ("model" or "thinking" or "new" or "name" or "extensions")) Composer.Focus(FocusState.Programmatic);
        }
    }

    private void ShowCommandNotice(string text, bool failure = false)
    {
        CommandNotice.Message = text;
        CommandNotice.Severity = failure ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
        CommandNotice.IsOpen = true;
        RefreshCommandSurface();
    }

    private void OnCommandNoticeClosed(InfoBar sender, InfoBarClosedEventArgs args) => RefreshCommandSurface();
    private void RefreshCommandSurface() => CommandSurface.Visibility = CommandNotice.IsOpen || CommandPanel.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

    private async Task ShowCommandDialogAsync(string title, UIElement content) => await new Controls.ActionContentDialog
    {
        XamlRoot = XamlRoot, Title = title, CloseButtonText = "Close",
        Content = new ScrollViewer { Content = content, MaxHeight = 500, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
    }.ShowAsync();

    private async Task<bool> ConfirmCommandAsync(string title, string description, UIElement input, string action)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(input);
        return await new Controls.ActionContentDialog
        {
            XamlRoot = XamlRoot, Title = title, Content = panel, PrimaryButtonText = action,
            CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary
        }.ShowAsync() == ContentDialogResult.Primary;
    }
}
