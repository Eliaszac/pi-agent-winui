using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class SettingsCategory(string title, params SettingsEntry[] entries) : ObservableObject
{
    private bool expanded = true;
    private bool visible = true;
    private bool? expansionBeforeSearch;
    public string Title { get; } = title;
    public IReadOnlyList<SettingsEntry> Entries { get; } = entries;
    public IReadOnlyDictionary<string, SettingsEntry> Items { get; } = entries.ToDictionary(entry => entry.Key);
    public SettingsEntry this[string key] => Items[key];
    public bool Expanded { get => expanded; set => SetProperty(ref expanded, value); }
    public bool Visible { get => visible; private set => SetProperty(ref visible, value); }

    public void Filter(string query)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in Entries)
            entry.Visible = words.All(word => (Title + " " + entry.Key + " " + entry.Keywords).Contains(word, StringComparison.OrdinalIgnoreCase));
        Visible = Entries.Any(entry => entry.Visible);
        if (words.Length > 0)
        {
            expansionBeforeSearch ??= Expanded;
            if (Visible) Expanded = true;
        }
        else if (expansionBeforeSearch is { } previous)
        {
            Expanded = previous;
            expansionBeforeSearch = null;
        }
    }
}
