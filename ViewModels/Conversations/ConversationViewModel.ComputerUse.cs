using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    private readonly HashSet<string> activeComputerTools = [];
    public bool IsComputerUseActive => connected && running && !IsRemoteTarget && activeComputerTools.Count > 0;
    public string ComputerUseLabel => stopping ? "Stopping computer use…" : "Computer use active";

    private void ObserveComputerUse(Models.Conversations.ConversationUpdate update)
    {
        if (update.History is not null || update.IsRunning == false || update.IsConnected == false || update.TurnCompleted)
        { activeComputerTools.Clear(); return; }
        if (update.Entry is { IsTool: true } entry && ComputerUseSupport.IsTool(entry.Speaker))
        {
            if (entry.Status == "Running") activeComputerTools.Add(entry.Id);
            else activeComputerTools.Remove(entry.Id);
        }
    }
}
