namespace PiAgentGui.ViewModels.Extensions;

public sealed record ExtensionGroup(string Name, string Description, IReadOnlyList<ExtensionCardViewModel> Cards);
