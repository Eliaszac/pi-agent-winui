using Microsoft.UI.Xaml.Input;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.Terminal;
using PiAgentGui.Utilities;

namespace PiAgentGui.Views;

public sealed partial class MainPage
{
    private readonly Dictionary<Guid, SidePanelState> sidePanels = [];
    private SidePanelState? activeSidePanels;
    private bool applyingSidePanel;
    private bool closingSidePanels;

    private void InitializeSidePanels()
    {
        TerminalPane.UseSharedTabs();
        ViewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ViewModel.Chat)) SwitchSidePanels();
        };
        Terminals.PropertyChanged += (_, change) =>
        {
            if (applyingSidePanel) return;
            if (change.PropertyName == nameof(Terminals.IsOpen) && !Terminals.IsOpen) PanelHidden("terminal");
            if (change.PropertyName is nameof(Terminals.Selected) or nameof(Terminals.IsOpen)
                && Terminals.IsOpen && Terminals.Selected is { } tab && tab.ConversationId == ViewModel.Chat?.ResearchOwnerId)
            {
                activeSidePanels?.Open("terminal", tab.Title, tab);
                ApplySidePanel();
            }
        };
        Files.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Files.IsOpen) && !Files.IsOpen) PanelHidden("files"); };
        SourceControl.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SourceControl.IsOpen) && !SourceControl.IsOpen) PanelHidden("source"); };
        Processes.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Processes.IsOpen) && !Processes.IsOpen) PanelHidden("processes"); };
        Capabilities.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Capabilities.IsOpen) && !Capabilities.IsOpen) PanelHidden("capabilities"); };
        Research.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Research.IsOpen) && !Research.IsOpen) PanelHidden("research");
            if (e.PropertyName == nameof(Research.Enabled) && !Research.Enabled)
            {
                foreach (var state in sidePanels.Values)
                    foreach (var tab in state.Tabs.Where(tab => tab.Kind == "research").ToArray()) state.Close(tab);
                ApplySidePanel();
            }
        };
        SwitchSidePanels();
    }

    private void PanelHidden(string kind)
    {
        if (applyingSidePanel || activeSidePanels?.Selected?.Kind != kind) return;
        activeSidePanels.IsOpen = false;
        ApplySidePanel();
    }

    private void SwitchSidePanels()
    {
        applyingSidePanel = true;
        try
        {
            var id = ViewModel.Chat?.ResearchOwnerId;
            activeSidePanels = id is { } owner ? sidePanels.GetValueOrDefault(owner) : null;
            if (id is { } key && activeSidePanels is null)
            {
                sidePanels[key] = activeSidePanels = new();
                ViewModel.Chat!.Closing += () => CloseConversationPanelsAsync(key);
            }
            Terminals.ConversationId = id;
            Terminals.Target = ViewModel.SelectedTarget;
            SidePanelTabs.TabItemsSource = activeSidePanels?.Tabs;
            Files.SelectTarget(ViewModel.SelectedTarget);
            SourceControl.SelectTarget(ViewModel.SelectedTarget);
            Research.SelectConversation(id);
            Capabilities.Select(ViewModel.Chat?.IsRemoteTarget == true ? null : ViewModel.Chat);
        }
        finally { applyingSidePanel = false; }
        ApplySidePanel();
    }

    private bool PanelAvailable(string kind) => ViewModel.Chat is not null &&
        (kind is "terminal" or "files" or "source" || ViewModel.SelectedTarget?.IsLocal != false && (kind != "research" || Research.Enabled));

    private void OpenSidePanel(string kind)
    {
        if (!PanelAvailable(kind) || activeSidePanels is null) return;
        if (kind == "terminal")
        {
            if (ViewModel.SelectedTarget is not { } target) return;
            try
            {
                applyingSidePanel = true;
                Terminals.Target = target;
                Terminals.Add(target.Path);
                if (Terminals.Selected is { } terminal) activeSidePanels.Open(kind, terminal.Title, terminal);
            }
            catch (Exception error) { ViewModel.Chat?.ReportAttachmentError(error.Message); }
            finally { applyingSidePanel = false; }
        }
        else activeSidePanels.Open(kind, SidePanelCatalog.Title(kind));
        ApplySidePanel();
    }

    private void ApplySidePanel()
    {
        if (applyingSidePanel) return;
        applyingSidePanel = true;
        try
        {
            var tab = activeSidePanels?.IsOpen == true ? activeSidePanels.Selected : null;
            SidePanelTabs.SelectedItem = activeSidePanels?.Selected;
            if (tab?.Terminal is { } terminal) Terminals.Show(terminal); else Terminals.Hide();
            Files.IsOpen = tab?.Kind == "files";
            SourceControl.IsOpen = tab?.Kind == "source";
            Processes.IsOpen = tab?.Kind == "processes";
            Capabilities.IsOpen = tab?.Kind == "capabilities";
            Research.IsOpen = tab?.Kind == "research" && Research.Enabled;
            PanelLauncher.Visibility = activeSidePanels?.Tabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BuildPanelLauncher();
        }
        finally { applyingSidePanel = false; }
        UpdateTerminalLayout();
    }

    private void OnSideTabSelected(object sender, SelectionChangedEventArgs args)
    {
        if (applyingSidePanel || activeSidePanels is null || SidePanelTabs.SelectedItem is not SidePanelTab tab) return;
        activeSidePanels.Selected = tab;
        ApplySidePanel();
    }

    private async void OnSideTabClosed(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (activeSidePanels is not { } owner || args.Item is not SidePanelTab tab) return;
        owner.Close(tab);
        ApplySidePanel();
        if (tab.Terminal is { } terminal)
        {
            try { await Terminals.CloseAsync(terminal); }
            catch (Exception error) { ViewModel.Chat?.ReportAttachmentError("Could not close terminal: " + error.Message); }
        }
    }

    private void OnHideSidePanel(object sender, RoutedEventArgs args)
    {
        if (activeSidePanels is not null) activeSidePanels.IsOpen = false;
        ApplySidePanel();
    }
    private void OnShowSidePanels(object sender, RoutedEventArgs args)
    {
        if (activeSidePanels is not null) activeSidePanels.IsOpen = true;
        ApplySidePanel();
    }

    private Task CloseConversationPanelsAsync(Guid id)
    {
        if (closingSidePanels) return Task.CompletedTask;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (sidePanels.Remove(id, out var state) && ReferenceEquals(activeSidePanels, state))
                {
                    activeSidePanels = null;
                    SidePanelTabs.TabItemsSource = null;
                    ApplySidePanel();
                }
                foreach (var tab in Terminals.Tabs.Where(tab => tab.ConversationId == id).ToArray()) await Terminals.CloseAsync(tab);
                completed.TrySetResult();
            }
            catch (Exception error) { ViewModel.Chat?.ReportAttachmentError("Could not close conversation terminals: " + error.Message); completed.TrySetResult(); }
        })) completed.TrySetResult();
        return completed.Task;
    }

    private void OnAddSideTab(TabView sender, object args)
    {
        var menu = new MenuFlyout();
        foreach (var kind in SidePanelCatalog.Kinds.Where(PanelAvailable))
        {
            var item = new Controls.ActionMenuFlyoutItem { Text = kind == "terminal" ? "New terminal" : SidePanelCatalog.Title(kind), Icon = new FontIcon { Glyph = SidePanelCatalog.Glyph(kind) } };
            item.Click += (_, _) => OpenSidePanel(kind);
            menu.Items.Add(item);
        }
        menu.ShowAt(sender);
    }

    private void BuildPanelLauncher()
    {
        PanelLauncher.Children.Clear();
        if (activeSidePanels?.Tabs.Count != 0) return;
        foreach (var kind in SidePanelCatalog.Kinds.Where(PanelAvailable))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new FontIcon { Glyph = SidePanelCatalog.Glyph(kind), FontSize = 14 });
            row.Children.Add(new TextBlock { Text = SidePanelCatalog.Title(kind) });
            var button = new Controls.ActionButton { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12) };
            button.Click += (_, _) => OpenSidePanel(kind);
            PanelLauncher.Children.Add(button);
        }
    }

    private void OnSideTabRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is not TabViewItem { DataContext: SidePanelTab { Terminal: not null } tab } item) return;
        var menu = new MenuFlyout();
        var rename = new Controls.ActionMenuFlyoutItem { Text = "Rename terminal" };
        rename.Click += async (_, _) => await RenameSideTabAsync(tab);
        menu.Items.Add(rename); menu.ShowAt(item); args.Handled = true;
    }

    private async void OnSideTabKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.F2 || sender is not TabViewItem { DataContext: SidePanelTab { Terminal: not null } tab }) return;
        args.Handled = true;
        await RenameSideTabAsync(tab);
    }

    private bool renamingSideTab;
    private async Task RenameSideTabAsync(SidePanelTab tab)
    {
        if (renamingSideTab) return;
        renamingSideTab = true;
        try
        {
            var input = new TextBox { Text = tab.Title, MaxLength = 80, Header = "Terminal name" };
            var dialog = new Controls.ActionContentDialog { XamlRoot = XamlRoot, Title = "Rename terminal", Content = input, PrimaryButtonText = "Rename", CloseButtonText = "Cancel" };
            input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) tab.Title = input.Text;
        }
        finally { renamingSideTab = false; }
    }

}

