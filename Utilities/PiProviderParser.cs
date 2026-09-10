using System.Text.Json;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Utilities;

public static class PiProviderParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    public static IReadOnlyList<PiProvider> Parse(JsonElement data)
    {
        var providers = JsonSerializer.Deserialize<PiProvider[]>(data, Options);
        if (providers is null || providers.Any(provider => provider is null || string.IsNullOrWhiteSpace(provider.Id) ||
            string.IsNullOrWhiteSpace(provider.Name) || provider.Source is null || provider.Method is null || provider.LoginLabel is null ||
            provider.Models is null || provider.Models.Any(model => model is null || string.IsNullOrWhiteSpace(model.Id) ||
                string.IsNullOrWhiteSpace(model.Name) || model.Input is null || model.ContextWindow < 0 || model.MaxTokens < 0)) ||
            providers.Select(provider => provider.Id).Distinct(StringComparer.Ordinal).Count() != providers.Length)
            throw new InvalidDataException("Pi returned an invalid provider list. Update Pi and refresh.");
        return providers;
    }
}
