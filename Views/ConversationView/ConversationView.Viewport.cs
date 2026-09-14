using System.Runtime.CompilerServices;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    private readonly ConditionalWeakTable<ConversationViewModel, TranscriptViewportState> viewports = new();
    private TranscriptViewportState? restoringViewport;
    private int restoreAttempts;

    private void SaveViewport(ConversationViewModel? model)
    {
        if (model is null || restoringViewport is not null) return;
        var state = new TranscriptViewportState(followTail);
        if (!followTail && Transcript.ItemsPanelRoot is ItemsStackPanel { FirstVisibleIndex: >= 0 } panel
            && panel.FirstVisibleIndex < Transcript.Items.Count
            && Transcript.Items[panel.FirstVisibleIndex] is ChatEntryViewModel anchor
            && Transcript.ContainerFromItem(anchor) is FrameworkElement container)
            state = new(false, anchor.Id, container.TransformToVisual(Transcript).TransformPoint(new()).Y);
        viewports.Remove(model); viewports.Add(model, state);
    }

    private void RestoreViewport(ConversationViewModel? model)
    {
        var state = model is not null && viewports.TryGetValue(model, out var saved) ? saved : new();
        followTail = state.FollowTail;
        initialTailPending = followTail;
        restoringViewport = !state.FollowTail && state.AnchorId is not null ? state : null;
        restoreAttempts = 0;
    }

    private bool TryRestoreViewport()
    {
        if (restoringViewport is not { } state || ViewModel is null) return false;
        var anchor = ViewModel.DisplayEntries.FirstOrDefault(entry => entry.Id == state.AnchorId);
        if (anchor is null || ++restoreAttempts > 4) { restoringViewport = null; return false; }
        if (Transcript.ContainerFromItem(anchor) is not FrameworkElement container)
        {
            Transcript.ScrollIntoView(anchor, ScrollIntoViewAlignment.Leading);
            return true;
        }
        var top = container.TransformToVisual(Transcript).TransformPoint(new()).Y;
        scroller?.ChangeView(null, Math.Max(0, scroller.VerticalOffset + top - state.AnchorTop), null, true);
        restoringViewport = null;
        return true;
    }
}
