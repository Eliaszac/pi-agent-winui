using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.ViewModels.Providers;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace PiAgentGui.Controls;

public sealed partial class ModelPicker : UserControl
{
    private ConversationViewModel? workspace;
    public void Open()
    {
        if (!IsEnabled) return;
        PickerButton.Focus(FocusState.Programmatic);
        PickerFlyout.ShowAt(PickerButton);
    }
    public ModelPicker()
    {
        InitializeComponent();
        IsEnabledChanged += (_, _) => { if (!IsEnabled) PickerFlyout.Hide(); };
        Unloaded += (_, _) => { PickerFlyout.Hide(); workspace = null; PickerContent.DataContext = null; };
    }

    public void Synchronize(ConversationViewModel? value)
    {
        if (!ReferenceEquals(workspace, value)) PickerFlyout.Hide();
        workspace = value;
        CurrentModel.Text = value?.SelectedModel is { } model ? $"Model: {model.Name}" : "Select model";
        ToolTipService.SetToolTip(PickerButton, value?.SelectedModel?.DisplayName ?? "Select model");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PickerButton, CurrentModel.Text);
        value?.ModelPicker?.Update(value.AvailableModels, value.SelectedModel);
        PickerContent.DataContext = value?.ModelPicker;
    }

    private async void OnOpening(object sender, object args)
    {
        var picker = workspace?.ModelPicker;
        if (picker is null) return;
        if (XamlRoot is { } root)
        {
            PickerContent.Width = Math.Clamp(root.Size.Width - 48, 240, 560);
            PickerContent.Height = Math.Clamp(root.Size.Height - 100, 160, 400);
            ProviderColumn.Width = new GridLength(PickerContent.Width < 440 ? 120 : 176);
        }
        await picker.OpenAsync();
    }

    private void OnOpened(object sender, object args) => SearchBox.Focus(FocusState.Programmatic);

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Down && ModelList.Items.Count > 0) { FocusModel(0); args.Handled = true; }
    }

    private void OnModelsKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is not (VirtualKey.Down or VirtualKey.Up) || ModelList.Items.Count == 0) return;
        if (FocusManager.GetFocusedElement(XamlRoot) is not FrameworkElement { DataContext: ModelPickerItem item }) return;
        var index = ModelList.Items.IndexOf(item);
        if (index < 0) return;
        FocusModel(Math.Clamp(index + (args.Key == VirtualKey.Down ? 1 : -1), 0, ModelList.Items.Count - 1));
        args.Handled = true;
    }

    private void FocusModel(int index)
    {
        ModelList.ScrollIntoView(ModelList.Items[index]);
        ModelList.UpdateLayout();
        if (ModelList.ContainerFromIndex(index) is DependencyObject container)
            VisualTreeSearch.FindDescendant<ActionButton>(container)?.Focus(FocusState.Keyboard);
    }

    private void OnSelectModel(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: ModelPickerItem item } || workspace?.CanChangeModel != true) return;
        workspace.SelectModelCommand.Execute(item.Model);
        PickerFlyout.Hide();
    }

    private async void OnFavorite(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement { DataContext: ModelPickerItem item } || workspace?.ModelPicker is not { } picker) return;
        await picker.ToggleFavoriteAsync(item);
        if (ReferenceEquals(picker, workspace?.ModelPicker) && !picker.Items.Contains(item)) SearchBox.Focus(FocusState.Programmatic);
    }
}
