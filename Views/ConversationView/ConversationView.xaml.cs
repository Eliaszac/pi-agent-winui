using System.ComponentModel;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ConversationViewModel), typeof(ConversationView), new PropertyMetadata(null, OnViewModelChanged));
    private ConversationViewModel? observed;
    private ScrollViewer? scroller;
    private bool followTail = true;
    private bool tailScrollPending;
    private bool composing;
    private bool synchronizingModel;
    private bool synchronizingThinking;
    private bool synchronizingApproval;
    public event EventHandler? ApprovalSetupRequested;
    private void OnFileDiffExpanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        followTail = false;
        tailScrollPending = false;
        if (sender.DataContext is ChangedFileViewModel file && sender.Content is StackPanel { Children.Count: 1 } panel)
            panel.Children.Add(new Controls.FileDiffView { Patch = file.Patch });
    }
    private void OnFileDiffCollapsed(Expander sender, ExpanderCollapsedEventArgs args)
    {
        followTail = false;
        tailScrollPending = false;
    }
    private void OnApprovalSetupClicked(object sender, RoutedEventArgs args) => ApprovalSetupRequested?.Invoke(this, EventArgs.Empty);
    private void OnCopyMessageClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: ChatEntryViewModel entry } button || !(entry.CanCopyUser || entry.CanCopyResponse)) return;
        try
        {
            var content = new DataPackage();
            content.SetText(entry.Text);
            Clipboard.SetContent(content);
            if (button is Controls.CopyFeedbackButton feedback) feedback.ShowCopied();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            ToolTipService.SetToolTip(button, "Couldn't copy. Try again.");
        }
    }
    private void OnApprovalSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (synchronizingApproval) return;
        if (sender is ComboBox { SelectedItem: string mode } && ViewModel?.CanChangeApprovalMode == true)
            ViewModel.SelectApprovalModeCommand.Execute(mode);
    }
    public ConversationViewModel? ViewModel
    {
        get => (ConversationViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public ConversationView()
    {
        InitializeComponent();
        PromptRail.PromptSelected += entry =>
        {
            followTail = false;
            tailScrollPending = false;
            Transcript.ScrollIntoView(entry, ScrollIntoViewAlignment.Leading);
        };
    }

    private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (ConversationView)sender;
        view.Root.DataContext = args.NewValue;
        view.followTail = true;
        view.tailScrollPending = false;
        view.PromptRail.Reset();
        view.ResetCommands();
        view.Observe();
    }

    private void Observe()
    {
        if (observed is not null)
        {
            observed.TranscriptChanged -= OnTranscriptChanged;
            observed.PropertyChanged -= OnPresentationChanged;
            observed.HandleComposerCommand = null;
        }
        observed = IsLoaded ? ViewModel : null;
        if (observed is not null)
        {
            observed.TranscriptChanged += OnTranscriptChanged;
            observed.PropertyChanged += OnPresentationChanged;
            observed.HandleComposerCommand = HandleTypedCommandAsync;
        }
        SynchronizeModelSelector();
        SynchronizeThinkingSelector();
        SynchronizeApprovalSelector();
        OnTranscriptChanged();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        scroller = Controls.VisualTreeSearch.FindDescendant<ScrollViewer>(Transcript);
        if (scroller is not null) scroller.ViewChanged += OnScrollChanged;
        Observe();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (observed is not null)
        {
            observed.TranscriptChanged -= OnTranscriptChanged;
            observed.PropertyChanged -= OnPresentationChanged;
            observed.HandleComposerCommand = null;
        }
        observed = null;
        ResetCommands();
        if (scroller is not null) scroller.ViewChanged -= OnScrollChanged;
        scroller = null;
    }

    private void OnScrollChanged(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        if (scroller is not null && !tailScrollPending) followTail = scroller.ScrollableHeight - scroller.VerticalOffset < 48;
    }

    private void OnTranscriptChanged()
    {
        PromptRail.Update(ViewModel?.Entries ?? []);
        if (!followTail || ViewModel?.DisplayEntries.Count is not > 0) return;
        tailScrollPending = true;
        // Do not first jump to the message's top; the next layout scrolls directly to the tail.
        Transcript.InvalidateMeasure();
    }

    private void OnMessageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        // Markdown is coalesced after transcript events, so follow its final measured height too.
        if (followTail) tailScrollPending = true;
    }

    private void OnTranscriptLayoutUpdated(object? sender, object args)
    {
        if (!tailScrollPending) return;
        scroller ??= Controls.VisualTreeSearch.FindDescendant<ScrollViewer>(Transcript);
        scroller?.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true);
        tailScrollPending = false;
    }

    private void OnSendInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (composing) return;
        args.Handled = true;
        if (FileReferencePanel.Visibility == Visibility.Visible) { AcceptFileReference(); return; }
        if (CommandPanel.Visibility == Visibility.Visible && CommandList.Items.Count > 0) { AcceptCommand(); return; }
        if (ViewModel?.CanSend == true) ViewModel.SendCommand.Execute(null);
    }

    private void OnNewLineInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (composing) return;
        args.Handled = true;
        var caret = Composer.SelectionStart;
        Composer.SelectedText = "\r";
        Composer.Select(caret + 1, 0);
    }

    private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (synchronizingModel) return;
        if (sender is ComboBox { SelectedIndex: >= 0 } selector && ViewModel is { CanChangeModel: true } viewModel
            && selector.SelectedIndex < viewModel.AvailableModels.Count)
            viewModel.SelectModelCommand.Execute(viewModel.AvailableModels[selector.SelectedIndex]);
    }

    private void OnPresentationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ConversationViewModel.IsRunning) && ViewModel?.IsRunning == true) DismissPicker();
        if (args.PropertyName == nameof(ConversationViewModel.IsConnected) && ViewModel?.IsConnected != true) ResetCommands();
        if (args.PropertyName is nameof(ConversationViewModel.ModelOptions) or nameof(ConversationViewModel.SelectedModelIndex)
            or nameof(ConversationViewModel.IsReady))
            SynchronizeModelSelector();
        if (args.PropertyName is nameof(ConversationViewModel.ThinkingLevels) or nameof(ConversationViewModel.SelectedThinkingLevel)
            or nameof(ConversationViewModel.IsReady))
            SynchronizeThinkingSelector();
        if (args.PropertyName is nameof(ConversationViewModel.SelectedApprovalMode) or nameof(ConversationViewModel.HasApprovalModes)
            or nameof(ConversationViewModel.IsReady))
            SynchronizeApprovalSelector();
    }

    private void OnModelSelectorLoaded(object sender, RoutedEventArgs args) => SynchronizeModelSelector();

    private void SynchronizeModelSelector()
    {
        // Apply the item source before selection, including when the initially collapsed composer loads.
        // Independent bindings can clear selection while WinUI replaces the item source.
        synchronizingModel = true;
        try
        {
            var viewModel = ViewModel;
            if (!ReferenceEquals(ModelSelector.ItemsSource, viewModel?.ModelOptions))
                ModelSelector.ItemsSource = viewModel?.ModelOptions;
            var index = viewModel?.SelectedModelIndex ?? -1;
            ModelSelector.SelectedIndex = index >= 0 && index < ModelSelector.Items.Count ? index : -1;
        }
        finally { synchronizingModel = false; }
    }

    private void OnThinkingSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (synchronizingThinking) return;
        if (sender is ComboBox { SelectedItem: string level } && ViewModel?.CanChangeThinkingLevel == true)
            ViewModel.SelectThinkingLevelCommand.Execute(level);
    }

    private void OnThinkingSelectorLoaded(object sender, RoutedEventArgs args) => SynchronizeThinkingSelector();

    private void OnApprovalSelectorLoaded(object sender, RoutedEventArgs args) => SynchronizeApprovalSelector();

    private void SynchronizeApprovalSelector()
    {
        synchronizingApproval = true;
        try
        {
            var viewModel = ViewModel;
            if (!ReferenceEquals(ApprovalSelector.ItemsSource, viewModel?.ApprovalModes))
                ApprovalSelector.ItemsSource = viewModel?.ApprovalModes;
            ApprovalSelector.SelectedItem = viewModel?.SelectedApprovalMode;
        }
        finally { synchronizingApproval = false; }
    }

    private void SynchronizeThinkingSelector()
    {
        synchronizingThinking = true;
        try
        {
            var viewModel = ViewModel;
            if (!ReferenceEquals(ThinkingSelector.ItemsSource, viewModel?.ThinkingLevels))
                ThinkingSelector.ItemsSource = viewModel?.ThinkingLevels;
            ThinkingSelector.SelectedItem = viewModel?.SelectedThinkingLevel;
        }
        finally { synchronizingThinking = false; }
    }

    private void OnCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args) => composing = true;
    private void OnCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args) { composing = false; QueuePickerUpdate(); }
}
