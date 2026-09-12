namespace PiAgentGui.Models.Pi;

/// <summary>A provider-scoped model identity, independent of its display name.</summary>
public sealed record ModelIdentity(string Provider, string Id)
{
    public static ModelIdentity From(PiModel model) => new(model.Provider, model.Id);
}
