namespace PiAgentGui.Models.Pi;

/// <summary>Non-secret provider metadata returned by the bundled management integration.</summary>
public sealed record PiProvider(string Id, string Name, bool Configured, string Source, string Method,
    bool Stored, bool Oauth, bool ApiKey, string LoginLabel, IReadOnlyList<PiProviderModel> Models);
