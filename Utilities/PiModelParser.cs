using System.Text.Json;
using PiAgentGui.Models.Pi;

namespace PiAgentGui.Utilities;

public static class PiModelParser
{
    public static PiModel Parse(JsonElement value)
    {
        var provider = PiJson.Text(value, "provider");
        var id = PiJson.Text(value, "id");
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(id))
            throw new InvalidDataException("Pi returned an invalid model.");
        var name = PiJson.Text(value, "name");
        return new(provider, id, string.IsNullOrWhiteSpace(name) ? id : name);
    }

    public static IReadOnlyList<PiModel> ParseList(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Pi returned an invalid model list.");
        return value.EnumerateArray().Select(Parse).DistinctBy(model => (model.Provider, model.Id)).ToArray();
    }
}
