using System.Text.Json;
using PiAgentGui.Models.Conversations;

namespace PiAgentGui.Utilities;

/// <summary>Reads lsp-pi 1.0.5 structured responses; never infers success from prose or silence.</summary>
public static class LspDiagnosticsParser
{
    public static IReadOnlyList<FileDiagnostics>? Parse(string tool, JsonElement input, JsonElement details)
    {
        if (tool != "lsp" || PiJson.Text(input, "severity") is not ("" or "all")) return null;
        var results = new List<FileDiagnostics>();
        if (PiJson.Text(input, "action") == "diagnostics")
        {
            if (!PiJson.Flag(details, "receivedResponse") || PiJson.Flag(details, "unsupported") || PiJson.Text(details, "error").Length > 0) return null;
            if (Read(PiJson.Text(input, "file"), PiJson.Field(details, "diagnostics")) is { } result) results.Add(result);
        }
        else if (PiJson.Text(input, "action") == "workspace-diagnostics")
        {
            var items = PiJson.Field(details, "items");
            if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 1000) return null;
            foreach (var item in items.EnumerateArray())
                if (PiJson.Text(item, "status") == "ok" && PiJson.Text(item, "error").Length == 0
                    && Read(PiJson.Text(item, "file"), PiJson.Field(item, "diagnostics")) is { } result) results.Add(result);
        }
        return results.Count > 0 ? results : null;
    }

    private static FileDiagnostics? Read(string path, JsonElement diagnostics)
    {
        if (string.IsNullOrWhiteSpace(path) || diagnostics.ValueKind != JsonValueKind.Array || diagnostics.GetArrayLength() > 100_000) return null;
        var errors = 0; var warnings = 0;
        foreach (var diagnostic in diagnostics.EnumerateArray())
        {
            // Severity is optional in LSP; without it we cannot reliably count errors/warnings.
            var severity = PiJson.Field(diagnostic, "severity");
            if (severity.ValueKind != JsonValueKind.Number || !severity.TryGetInt32(out var value) || value is < 1 or > 4) return null;
            if (value == 1) errors++;
            if (value == 2) warnings++;
        }
        return new(path, errors, warnings);
    }
}
