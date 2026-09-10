using System.ComponentModel;
using Microsoft.UI.Xaml.Input;
using PiAgentGui.ViewModels.Projects;
using Windows.System;

namespace PiAgentGui.Controls;

public sealed partial class InlineNameEditor : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel),
        typeof(InlineRenameViewModel), typeof(InlineNameEditor), new PropertyMetadata(null, OnViewModelChanged));
    private InlineRenameViewModel? observed;
    public InlineRenameViewModel? ViewModel
    {
        get => (InlineRenameViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public InlineNameEditor() => InitializeComponent();
    private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var editor = (InlineNameEditor)sender;
        editor.Root.DataContext = args.NewValue;
        editor.Observe();
    }
    private void OnLoaded(object sender, RoutedEventArgs args) => Observe();
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (observed is not null) observed.PropertyChanged -= OnStateChanged;
        observed = null;
    }
    private void Observe()
    {
        if (observed is not null) observed.PropertyChanged -= OnStateChanged;
        observed = IsLoaded ? ViewModel : null;
        if (observed is not null) observed.PropertyChanged += OnStateChanged;
        FocusEditor();
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(InlineRenameViewModel.IsEditing)) FocusEditor();
    }
    private void FocusEditor()
    {
        if (ViewModel?.IsEditing != true) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel?.IsEditing != true || !IsLoaded) return;
            NameBox.Focus(FocusState.Programmatic);
            NameBox.SelectAll();
        });
    }
    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter) { args.Handled = true; ViewModel?.SaveCommand.Execute(null); }
        else if (args.Key == VirtualKey.Escape) { args.Handled = true; ViewModel?.CancelCommand.Execute(null); }
    }
    private void OnLostFocus(object sender, RoutedEventArgs args)
    {
        if (ViewModel?.IsEditing == true && ViewModel.CanEdit && !ViewModel.HasError) ViewModel.SaveCommand.Execute(null);
    }
}
