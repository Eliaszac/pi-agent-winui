using System.ComponentModel;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView : UserControl
{
    public static readonly DependencyProperty GettingStartedProperty = DependencyProperty.Register(nameof(GettingStarted),
        typeof(ViewModels.Shell.GettingStartedViewModel), typeof(ConversationView), new PropertyMetadata(null));
    public ViewModels.Shell.GettingStartedViewModel? GettingStarted
    {
        get => (ViewModels.Shell.GettingStartedViewModel?)GetValue(GettingStartedProperty);
        set => SetValue(GettingStartedProperty, value);
    }
    public event EventHandler? ProvidersRequested;
    private void OnConnectProvider(object sender, RoutedEventArgs args) => ProvidersRequested?.Invoke(this, EventArgs.Empty);
    private bool TryOpenProviderSetup()
    {
        if (GettingStarted?.NeedsProvider != true || ProvidersRequested is null) return false;
        ProvidersRequested.Invoke(this, EventArgs.Empty);
        return true;
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ConversationViewModel), typeof(ConversationView), new PropertyMetadata(null, OnViewModelChanged));
    private ConversationViewModel? observed;
    private ScrollViewer? scroller;
    private bool followingTail = true;
    private bool followTail
    {
        get => followingTail;
        set
        {
            followingTail = value;
            UpdateTranscriptAnchoring();
        }
    }
    private bool tailScrollPending;
    private bool initialTailPending = true;
    private object? realizingTail;
    private bool transcriptScrollInputPending;
    private double previousTranscriptOffset;
    private bool composing;
    private bool synchronizingThinking;
    private bool synchronizingApproval;
    public event EventHandler? ApprovalSetupRequested;
    private void OnFileDiffExpanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (!sender.IsLoaded) return;
        followTail = false;
        tailScrollPending = false;
    }
    private void OnFileDiffCollapsed(Expander sender, ExpanderCollapsedEventArgs args)
    {
        if (!sender.IsLoaded) return;
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
        Transcript.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnTranscriptScrollInput), true);
        Transcript.AddHandler(PointerPressedEvent, new PointerEventHandler(OnTranscriptPointerPressed), true);
        Transcript.AddHandler(KeyDownEvent, new KeyEventHandler(OnTranscriptScrollKey), true);
        PromptRail.PromptSelected += entry =>
        {
            JumpToPrompt(entry);
        };
    }

    private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (ConversationView)sender;
        view.SaveViewport(args.OldValue as ConversationViewModel);
        view.Root.DataContext = args.NewValue;
        view.RestoreViewport(args.NewValue as ConversationViewModel);
        view.tailScrollPending = false;
        view.realizingTail = null;
        view.transcriptScrollInputPending = false;
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
            observed.TryOpenProviderSetup = null;
        }
        observed = IsLoaded ? ViewModel : null;
        if (observed is not null)
        {
            observed.TranscriptChanged += OnTranscriptChanged;
            observed.PropertyChanged += OnPresentationChanged;
            observed.HandleComposerCommand = HandleTypedCommandAsync;
            observed.TryOpenProviderSetup = TryOpenProviderSetup;
        }
        SynchronizeModelSelector();
        SynchronizeThinkingSelector();
        SynchronizeApprovalSelector();
        OnTranscriptChanged();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachTranscriptScroller();
        previousTranscriptOffset = scroller?.VerticalOffset ?? 0;
        Observe();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        SaveViewport(observed);
        if (observed is not null)
        {
            observed.TranscriptChanged -= OnTranscriptChanged;
            observed.PropertyChanged -= OnPresentationChanged;
            observed.HandleComposerCommand = null;
            observed.TryOpenProviderSetup = null;
        }
        observed = null;
        ResetCommands();
        if (scroller is not null) scroller.ViewChanged -= OnScrollChanged;
        scroller = null;
    }

    private void OnScrollChanged(object? sender, ScrollViewerViewChangedEventArgs args)
    {
        if (scroller is null || restoringViewport is not null) return;
        // Layout, virtualization and ChangeView also raise ViewChanged. Only explicit
        // input may detach a follower; reaching the bottom reattaches a reader.
        var offset = scroller.VerticalOffset;
        if (!followTail && Utilities.TranscriptScrollPolicy.ShouldResumeFollowing(
                transcriptScrollInputPending, previousTranscriptOffset, offset, offset + TranscriptTailDistance()))
            followTail = true;
        previousTranscriptOffset = offset;
        if (!args.IsIntermediate) transcriptScrollInputPending = false;
    }

    private void OnTranscriptChanged()
    {
        PromptRail.Update(ViewModel?.Entries ?? []);
        if (!followTail || ViewModel?.DisplayEntries.Count is not > 0) return;
        tailScrollPending = true;
        // Collection changes already schedule layout. Do not invalidate the virtualized
        // history's measurement on every streamed token.
        Transcript.InvalidateArrange();
    }

    private void OnMarkdownContentRendered(object? sender, EventArgs args)
    {
        // Follow coalesced content changes, never local layout changes such as sorting a table.
        if (followTail && sender is FrameworkElement { DataContext: ChatEntryViewModel entry }
            && ViewModel?.DisplayEntries.LastOrDefault(item => item.IsLeftAligned) == entry)
        {
            tailScrollPending = true;
            Transcript.InvalidateArrange();
        }
    }

    private void OnTranscriptLayoutUpdated(object? sender, object args)
    {
        AttachTranscriptScroller();
        UpdateTranscriptAnchoring();
        if (TryRestoreViewport() || !followTail || ViewModel?.DisplayEntries.Count is not > 0) return;
        if (!tailScrollPending) return;
        // Only initial navigation may request a distant item. Streaming uses native
        // bottom anchoring, never a queued ScrollIntoView on each appended row.
        var last = ViewModel.DisplayEntries.LastOrDefault();
        if (last is null || scroller is null) return;
        if (Transcript.ContainerFromItem(last) is not FrameworkElement container)
        {
            if (initialTailPending && !ReferenceEquals(realizingTail, last))
            {
                realizingTail = last;
                Transcript.ScrollIntoView(last);
            }
            return;
        }
        realizingTail = null;
        if (!container.IsLoaded || container.ActualHeight <= 0) return;
        initialTailPending = false;
        tailScrollPending = false;
        // Use the realized row, not the estimated extent of virtualized history.
        var bottom = container.TransformToVisual(scroller).TransformPoint(new()).Y + container.ActualHeight;
        var correction = bottom - scroller.ViewportHeight;
        if (correction < 1) return;
        var offset = Utilities.TranscriptScrollPolicy.TailOffset(scroller.VerticalOffset, bottom, scroller.ViewportHeight, scroller.ScrollableHeight);
        if (Math.Abs(offset - scroller.VerticalOffset) < 1) return;
        scroller.ChangeView(null, offset, null, disableAnimation: true);
    }

    private void UpdateTranscriptAnchoring()
    {
        if (Transcript?.ItemsPanelRoot is ItemsStackPanel panel)
        {
            var mode = followTail ? ItemsUpdatingScrollMode.KeepLastItemInView : ItemsUpdatingScrollMode.KeepItemsInView;
            if (panel.ItemsUpdatingScrollMode != mode) panel.ItemsUpdatingScrollMode = mode;
        }
    }

    private void AttachTranscriptScroller()
    {
        if (scroller is not null) return;
        scroller = Controls.VisualTreeSearch.FindDescendant<ScrollViewer>(Transcript);
        if (scroller is null) return;
        scroller.BringIntoViewOnFocusChange = false;
        scroller.ViewChanged += OnScrollChanged;
    }

    private double TranscriptTailDistance()
    {
        var last = ViewModel?.DisplayEntries.LastOrDefault();
        if (scroller is null || last is null || Transcript.ContainerFromItem(last) is not FrameworkElement { IsLoaded: true } container)
            return double.PositiveInfinity;
        return container.TransformToVisual(scroller).TransformPoint(new()).Y + container.ActualHeight - scroller.ViewportHeight;
    }

    private void OnTranscriptScrollInput(object sender, PointerRoutedEventArgs args)
    {
        if (!IsNestedTranscriptScroller(args.OriginalSource as DependencyObject))
            BeginTranscriptScrollInput(args.GetCurrentPoint(Transcript).Properties.MouseWheelDelta < 0);
    }

    private bool IsNestedTranscriptScroller(DependencyObject? source)
    {
        for (var element = source; element is not null && element != Transcript;
             element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            if (element is ScrollViewer viewer) return viewer != scroller;
        return false;
    }

    private void BeginTranscriptScrollInput(bool towardEnd = true)
    {
        DetachTranscriptTail();
        transcriptScrollInputPending = towardEnd;
        previousTranscriptOffset = scroller?.VerticalOffset ?? 0;
    }

    private void OnTranscriptPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (IsNestedTranscriptScroller(args.OriginalSource as DependencyObject)) return;
        // Mouse text selection is not scrolling. Touch/pen panning and scrollbar
        // dragging must interrupt following before the ScrollViewer changes offset.
        if (args.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            BeginTranscriptScrollInput();
            return;
        }
        for (var element = args.OriginalSource as DependencyObject; element is not null && element != Transcript;
             element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            if (element is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar)
            {
                BeginTranscriptScrollInput();
                return;
            }
    }

    private void OnTranscriptScrollKey(object sender, KeyRoutedEventArgs args)
    {
        if (IsNestedTranscriptScroller(args.OriginalSource as DependencyObject)) return;
        if (args.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down
            or Windows.System.VirtualKey.PageUp or Windows.System.VirtualKey.PageDown
            or Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End)
            BeginTranscriptScrollInput(args.Key is Windows.System.VirtualKey.Down
                or Windows.System.VirtualKey.PageDown or Windows.System.VirtualKey.End);
    }

    private void DetachTranscriptTail()
    {
        restoringViewport = null;
        followTail = false;
        tailScrollPending = false;
        transcriptScrollInputPending = false;
        realizingTail = null;
        initialTailPending = false;
    }
    private void OnSendInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (composing) return;
        args.Handled = true;
        if (FileReferencePanel.Visibility == Visibility.Visible) { AcceptFileReference(); return; }
        if (CommandPanel.Visibility == Visibility.Visible && CommandList.Items.Count > 0) { AcceptCommand(); return; }
        if (!Utilities.ComposerEnterBehavior.Sends(false, Controls.ReadingPreferences.Current.ControlEnterToSend)) { InsertNewLine(); return; }
        if (ViewModel?.CanSend == true) ViewModel.SendCommand.Execute(null);
    }

    private void OnNewLineInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (composing) return;
        args.Handled = true;
        if (Utilities.ComposerEnterBehavior.Sends(true, Controls.ReadingPreferences.Current.ControlEnterToSend))
        {
            if (ViewModel?.CanSend == true) ViewModel.SendCommand.Execute(null);
            return;
        }
        InsertNewLine();
    }

    private void InsertNewLine()
    {
        var caret = Composer.SelectionStart;
        Composer.SelectedText = "\r";
        Composer.Select(caret + 1, 0);
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
        ModelSelector.Synchronize(ViewModel);
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
