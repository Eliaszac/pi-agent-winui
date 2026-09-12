using Microsoft.UI.Xaml.Input;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;
using Windows.System;

namespace PiAgentGui.Views;

public sealed partial class CommandPaletteDialog : Controls.ActionContentDialog
{
    private readonly PaletteUsageStore? usage;
    private IReadOnlyList<PaletteCommand> commands = [];
    private readonly Stack<(IReadOnlyList<PaletteCommand> Commands, string Scope, string Id)> parents = new();
    public IEnumerable<string> UsageIds => parents.Select(parent => parent.Id).Append(SelectedCommand?.Id ?? "").Where(id => id.Length > 0).Distinct();
    public bool OpenProviders { get; private set; }
    public PaletteCommand? SelectedCommand { get; private set; }
    public CommandPaletteDialog() : this(false, [], null) { }
    public CommandPaletteDialog(bool connected, IReadOnlyList<PaletteCommand> commands, PaletteUsageStore? usage)
    {
        InitializeComponent();
        this.commands = commands; this.usage = usage;
        ProviderState.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        CommandsState.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        Opened += (_, _) => { if (connected) Search.Focus(FocusState.Programmatic); else ProviderAction.Focus(FocusState.Programmatic); };
        Refresh();
    }
    private void Refresh()
    {
        if (Results is null) return;
        var matches = usage?.Rank(commands, Search.Text) ?? [];
        Results.ItemsSource = matches;
        Results.SelectedIndex = matches.Count > 0 ? 0 : -1;
        NoResults.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnSearchChanged(object sender, TextChangedEventArgs args) => Refresh();
    private void OnCommandClicked(object sender, ItemClickEventArgs args) { if (args.ClickedItem is PaletteCommand command) Choose(command); }
    private void Choose(PaletteCommand command)
    {
        if (!command.CanUse) { Refresh(); return; }
        if (command.Children is { } children)
        {
            parents.Push((commands, ScopeLabel.Text, command.Id)); commands = children(); ScopeLabel.Text = command.Title;
            BackAction.Visibility = Visibility.Visible; Search.Text = ""; Refresh(); Search.Focus(FocusState.Programmatic);
        }
        else { SelectedCommand = command; Hide(); }
    }
    private void OnBack(object sender, RoutedEventArgs args)
    {
        if (!parents.TryPop(out var parent)) return;
        commands = parent.Commands; ScopeLabel.Text = parent.Scope; Search.Text = "";
        BackAction.Visibility = parents.Count > 0 ? Visibility.Visible : Visibility.Collapsed; Refresh(); Search.Focus(FocusState.Programmatic);
    }
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter && Results.SelectedItem is PaletteCommand command) { args.Handled = true; Choose(command); }
        else if (args.Key is VirtualKey.Down or VirtualKey.Up && Results.Items.Count > 0)
        {
            args.Handled = true;
            Results.SelectedIndex = (Results.SelectedIndex + (args.Key == VirtualKey.Down ? 1 : Results.Items.Count - 1)) % Results.Items.Count;
            Results.ScrollIntoView(Results.SelectedItem);
        }
    }
    private void OnResultsKeyDown(object sender, KeyRoutedEventArgs args) { if (args.Key == VirtualKey.Enter && Results.SelectedItem is PaletteCommand command) { args.Handled = true; Choose(command); } }
    private void OnOpenProviders(object sender, RoutedEventArgs args) { OpenProviders = true; Hide(); }
}
