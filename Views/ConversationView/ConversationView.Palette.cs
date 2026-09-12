using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Views;

public sealed partial class ConversationView
{
    public void FocusComposer() => Composer.Focus(FocusState.Programmatic);
    public void JumpToLatest() { followTail = true; tailScrollPending = true; Transcript.InvalidateMeasure(); }
    public void JumpToPrompt(ChatEntryViewModel entry)
    {
        followTail = false; tailScrollPending = false;
        Transcript.ScrollIntoView(entry, ScrollIntoViewAlignment.Leading);
    }
    public Task<bool> RunPaletteCommandAsync(string command) => HandleTypedCommandAsync(command);
}
