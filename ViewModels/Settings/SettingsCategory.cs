using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class SettingsCategory(string title, string keywords) : ObservableObject
{
    private bool expanded = true;
    private bool visible = true;
    private bool? expansionBeforeSearch;
    public string Title { get; } = title;
    public bool Expanded { get => expanded; set => SetProperty(ref expanded, value); }
    public bool Visible { get => visible; private set => SetProperty(ref visible, value); }

    public void Filter(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Visible = words.All(word => (Title + " " + keywords).Contains(word, StringComparison.OrdinalIgnoreCase));
        if (words.Length > 0) { expansionBeforeSearch ??= Expanded; if (Visible) Expanded = true; }
        else if (expansionBeforeSearch is { } previous) { Expanded = previous; expansionBeforeSearch = null; }
    }
}
