namespace PiAgentGui.Models.Conversations;

public sealed record PaletteCommand(string Id, string Title, string Description, Func<Task>? Execute = null,
    Func<IReadOnlyList<PaletteCommand>>? Children = null, Func<bool>? Available = null, string Aliases = "")
{
    public bool CanUse => Available?.Invoke() ?? true;
    public static PaletteCommand Action(string id, string title, string description, Action action, Func<bool>? available = null, string aliases = "") =>
        new(id, title, description, () => { action(); return Task.CompletedTask; }, Available: available, Aliases: aliases);
}
