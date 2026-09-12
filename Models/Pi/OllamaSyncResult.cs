namespace PiAgentGui.Models.Pi;

public sealed record OllamaSyncResult(Uri Endpoint, int ModelCount, int Added, IReadOnlyList<OllamaModel> NewModels, string? Warning, IReadOnlyList<string> Names);
