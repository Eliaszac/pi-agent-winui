using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class SettingsEntry(string key, string keywords) : ObservableObject
{
    private bool visible = true;
    public string Key { get; } = key;
    public string Keywords { get; } = keywords;
    public bool Visible { get => visible; internal set => SetProperty(ref visible, value); }
}
