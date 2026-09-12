namespace PiAgentGui.Models.Pi;

public sealed record OllamaRegistration(string ProviderId, Uri Endpoint, IReadOnlySet<string> ModelIds);
