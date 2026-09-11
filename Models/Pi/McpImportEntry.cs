using PiAgentGui.Utilities;

namespace PiAgentGui.Models.Pi;

/// <summary>A reviewable server definition; credential values are not included in its display summary.</summary>
public sealed class McpImportEntry(string name, string transport, string definition, bool conflict) : ObservableObject
{
    private bool selected = !conflict;
    public string Name { get; } = name;
    public string Transport { get; } = transport;
    public string Definition { get; } = definition;
    public bool CanImport => !conflict;
    public string Description => Transport + (conflict ? " · name already exists; choose a different name" : " · new global server");
    public bool Selected { get => selected; set => SetProperty(ref selected, value && CanImport); }
}
