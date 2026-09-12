using PiAgentGui.Models.Pi;

namespace PiAgentGui.ViewModels.Providers;

public sealed record OllamaModelChoice(OllamaModel Model, bool Imported)
{
    public string Name => Model.Id;
    public string Detail => Imported ? "Available in Pi · existing settings preserved"
        : Model.Error ?? "Tool calling is not supported";
    public string ContextDetail => Model.Tools && Model.Error is null
        ? $"Context: {Model.ImportContext:N0} tokens ({Model.ContextSource})" : "";
}
