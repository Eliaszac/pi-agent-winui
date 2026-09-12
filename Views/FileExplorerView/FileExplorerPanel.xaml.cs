using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PiAgentGui.Controls;
using PiAgentGui.ViewModels.Files;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace PiAgentGui.Views;

public sealed partial class FileExplorerPanel : UserControl
{
    private const string DragKey = "PiAgentGui.ProjectFile";
    private readonly Dictionary<TextBox, long> nameCallbacks = [];
    private readonly Dictionary<Image, long> iconCallbacks = [];
    public FileExplorerPanel() => InitializeComponent();
    private FileExplorerViewModel Model => (FileExplorerViewModel)DataContext;
    public Func<string, Task>? OpenFile { get; set; }
    private async void OnIconLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not Image image) return;
        image.ActualThemeChanged -= OnIconThemeChanged;
        image.ActualThemeChanged += OnIconThemeChanged;
        if (!iconCallbacks.ContainsKey(image)) iconCallbacks[image] = image.RegisterPropertyChangedCallback(TagProperty, async (_, _) => await UpdateIconAsync(image));
        await UpdateIconAsync(image);
    }
    private void OnIconUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is not Image image) return;
        image.ActualThemeChanged -= OnIconThemeChanged;
        if (iconCallbacks.Remove(image, out var token)) image.UnregisterPropertyChangedCallback(TagProperty, token);
    }
    private async void OnIconThemeChanged(FrameworkElement sender, object args) { if (sender is Image image) await UpdateIconAsync(image); }
    private async Task UpdateIconAsync(Image image)
    {
        if (image.Tag is not string icon) return;
        try { var theme = image.ActualTheme; var source = await FileTypeImageSource.CreateAsync(icon, theme); if (image.Tag as string == icon && image.ActualTheme == theme) image.Source = source; }
        catch (Exception exception) { Model.Report(exception); }
    }
    private async void OnRefresh(object sender, RoutedEventArgs args) => await Model.RefreshAsync();
    private void OnClose(object sender, RoutedEventArgs args) => Model.IsOpen = false;
    private async void OnToggle(object sender, RoutedEventArgs args) { if (sender is FrameworkElement { Tag: ExplorerItem item }) await Model.ToggleAsync(item); }
    private async void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    {
        for (var source = args.OriginalSource as DependencyObject; source is not null && !ReferenceEquals(source, sender); source = VisualTreeHelper.GetParent(source))
            if (source is ButtonBase or TextBox) { args.Handled = true; return; }
        if (sender is not FrameworkElement { Tag: ExplorerItem item } || Model.Editing is not null) return;
        args.Handled = true;
        if (!item.IsDirectory) await OpenAsync(item);
    }
    private async void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: ExplorerItem { IsDirectory: true } item } || Model.Editing is not null) return;
        for (var source = args.OriginalSource as DependencyObject; source is not null && !ReferenceEquals(source, sender); source = VisualTreeHelper.GetParent(source))
            if (source is ButtonBase or TextBox) return;
        args.Handled = true;
        FileList.SelectedItem = item;
        await Model.ToggleAsync(item);
    }
    private async Task OpenAsync(ExplorerItem item)
    {
        try { if (OpenFile is not null && !item.IsLinked) await OpenFile(item.Path); }
        catch (Exception exception) { Model.Report(exception); }
    }
    private void OnNameLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextBox box && !nameCallbacks.ContainsKey(box)) nameCallbacks[box] = box.RegisterPropertyChangedCallback(VisibilityProperty,
            (_, _) => { if (box.Visibility == Visibility.Visible) DispatcherQueue.TryEnqueue(() => { box.Focus(FocusState.Programmatic); box.SelectAll(); }); });
        if (sender is TextBox { Visibility: Visibility.Visible } visible) { visible.Focus(FocusState.Programmatic); visible.SelectAll(); }
    }
    private void OnNameUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is TextBox box && nameCallbacks.Remove(box, out var token)) box.UnregisterPropertyChangedCallback(VisibilityProperty, token);
    }
    private async void OnNameKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter) { args.Handled = true; await Model.CommitEditAsync(); }
        if (args.Key == VirtualKey.Escape) { args.Handled = true; Model.CancelEdit(); }
    }
    private async void OnListKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (Model.Editing is not null || FileList.SelectedItem is not ExplorerItem item) return;
        if (args.Key == VirtualKey.F2) { args.Handled = true; Model.BeginRename(item); }
        if (args.Key == VirtualKey.Delete) { args.Handled = true; await DeleteAsync(item); }
        if (args.Key == VirtualKey.Enter) { args.Handled = true; if (item.IsDirectory) await Model.ToggleAsync(item); else await OpenAsync(item); }
        if (args.Key == VirtualKey.Right && !item.IsExpanded || args.Key == VirtualKey.Left && item.IsExpanded)
        { args.Handled = true; await Model.ToggleAsync(item); }
    }
    private async Task DeleteAsync(ExplorerItem item)
    {
        if (Model.IsReadOnlyTarget) return;
        if (item.IsRoot || Model.IsBusy || Model.Editing is not null) return;
        try
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Move to Recycle Bin?", Content = item.Name,
                PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) await Model.DeleteAsync(item);
        }
        catch (Exception exception) { Model.Report(exception); }
    }
    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: ExplorerItem item }) return;
        args.Handled = true; FileList.SelectedItem = item;
        ShowItemMenu((FrameworkElement)sender, item, args);
    }

    private void OnBackgroundContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (args.Handled || Model.Root is not { } root) return;
        args.Handled = true;
        var item = Model.Items.FirstOrDefault(row => row.IsRoot) ?? new ExplorerItem(root, true, 0, true);
        ShowItemMenu(FileList, item, args);
    }

    private void ShowItemMenu(FrameworkElement anchor, ExplorerItem item, ContextRequestedEventArgs context)
    {
        var menu = new MenuFlyout();
        if (!item.IsLinked && !Model.IsReadOnlyTarget)
        {
            var file = new ActionMenuFlyoutItem { Text = "New file", IsEnabled = !Model.IsBusy && Model.Editing is null };
            file.Click += async (_, _) => await Model.BeginCreateAsync(item, false); menu.Items.Add(file);
            var folder = new ActionMenuFlyoutItem { Text = "New folder", IsEnabled = file.IsEnabled };
            folder.Click += async (_, _) => await Model.BeginCreateAsync(item, true); menu.Items.Add(folder);
            if (!item.IsDirectory) { var open = new ActionMenuFlyoutItem { Text = "Open in editor" }; open.Click += async (_, _) => await OpenAsync(item); menu.Items.Add(open); }
        }
        if (!item.IsRoot && !item.IsLinked && !Model.IsReadOnlyTarget)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var rename = new ActionMenuFlyoutItem { Text = "Rename", IsEnabled = !Model.IsBusy && Model.Editing is null };
            rename.Click += (_, _) => Model.BeginRename(item); menu.Items.Add(rename);
            var delete = new ActionMenuFlyoutItem { Text = "Delete", IsEnabled = rename.IsEnabled };
            delete.Click += async (_, _) => await DeleteAsync(item); menu.Items.Add(delete);
        }
        var refresh = new ActionMenuFlyoutItem { Text = "Refresh" }; refresh.Click += async (_, _) => await Model.RefreshAsync(); menu.Items.Add(refresh);
        if (Model.IsReadOnlyTarget && !item.IsDirectory && !item.IsLinked)
        {
            var preview = new ActionMenuFlyoutItem { Text = "Preview target file" };
            preview.Click += async (_, _) => await OpenAsync(item); menu.Items.Add(preview);
        }
        if (context.TryGetPosition(anchor, out var position)) menu.ShowAt(anchor, new FlyoutShowOptions { Position = position });
        else menu.ShowAt(anchor);
    }
    private void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (Model.IsReadOnlyTarget) { args.Cancel = true; return; }
        if (sender is not FrameworkElement { Tag: ExplorerItem item } || item.IsRoot || item.IsLinked || Model.IsBusy || Model.Editing is not null) { args.Cancel = true; return; }
        args.Data.Properties[DragKey] = item.Path; args.AllowedOperations = DataPackageOperation.Move;
    }
    private void OnDragOver(object sender, DragEventArgs args)
    {
        if (sender is FrameworkElement { Tag: ExplorerItem { IsDirectory: true, IsLinked: false } } && args.DataView.Properties.ContainsKey(DragKey) && !Model.IsBusy && Model.Editing is null)
        { args.AcceptedOperation = DataPackageOperation.Move; args.DragUIOverride.Caption = "Move into folder"; args.Handled = true; }
    }
    private async void OnDrop(object sender, DragEventArgs args)
    {
        if (sender is not FrameworkElement { Tag: ExplorerItem item } || !args.DataView.Properties.TryGetValue(DragKey, out var value) || value is not string source) return;
        args.Handled = true;
        var deferral = args.GetDeferral();
        try { await Model.MoveAsync(source, item); } finally { deferral.Complete(); }
    }
}
