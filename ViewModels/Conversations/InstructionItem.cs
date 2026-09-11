using PiAgentGui.Models.Conversations;

namespace PiAgentGui.ViewModels.Conversations;

public sealed record InstructionItem(LoadedInstruction File, string Scope, string Status, bool CanEdit)
{
    public string Name => System.IO.Path.GetFileName(File.Path);
}
