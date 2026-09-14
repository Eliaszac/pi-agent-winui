using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    public void FocusComposer() => Composer.Focus(FocusState.Programmatic);
    public void JumpToLatest() { restoringViewport = null; followTail = true; initialTailPending = true; realizingTail = null; tailScrollPending = true; Transcript.InvalidateArrange(); }
    public void JumpToPrompt(ChatEntryViewModel entry)
    {
        DetachTranscriptTail();
        Transcript.ScrollIntoView(entry, ScrollIntoViewAlignment.Leading);
    }
    public Task<bool> RunPaletteCommandAsync(string command) => HandleTypedCommandAsync(command);
}
