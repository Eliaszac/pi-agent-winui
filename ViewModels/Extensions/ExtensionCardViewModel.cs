using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Extensions;

public sealed class ExtensionCardViewModel : ObservableObject
{
    private string status = "Checking global configuration…";
    private bool needsSetup;
    private bool enabled;
    private bool saving;
    private bool loaded;
    public bool IsEnabled { get => enabled; private set => SetProperty(ref enabled, value); }
    public bool CanToggle => loaded && !saving;
    public bool ShowDetails => !Definition.Bundled;
    public string ToggleLabel => "Enable " + Definition.Name;
    public string ToggleHint => Definition.ToggleHint ?? (Definition.WriteEnabled is null ? "Applies before the next request. Turning off keeps existing recovery data."
        : "Applies next turn. Turning off cancels active research and keeps saved results.");
    public ExtensionDefinition Definition { get; }
    public string SetupLabel => Definition.Bundled ? "Manage" : "Set up";
    public string Attribution => Definition.Bundled ? "By Pi desktop · our extension" : $"Third-party · by {Definition.Author}";
    public string VersionLabel => $"{(Definition.RecommendationOnly ? "Recommended version" : "Supported version")} {Definition.Version}";
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public bool NeedsSetup { get => needsSetup; private set => SetProperty(ref needsSetup, value); }
    public AsyncRelayCommand RefreshCommand { get; }

    public ExtensionCardViewModel(ExtensionDefinition definition)
    {
        Definition = definition;
        RefreshCommand = new AsyncRelayCommand(async _ =>
        {
            if (saving) return;
            loaded = false;
            OnPropertyChanged(nameof(CanToggle));
            var installation = await Task.Run(definition.CheckInstallation);
            if (saving) return;
            if (definition.Bundled) IsEnabled = await Task.Run(definition.ReadEnabled ?? CheckpointSettings.IsEnabled);
            loaded = true;
            OnPropertyChanged(nameof(CanToggle));
            NeedsSetup = installation.NeedsSetup;
            Status = installation.Status;
        }, exception =>
        {
            NeedsSetup = true;
            Status = "Couldn't check configuration: " + exception.Message;
        });
    }

    public async Task SetEnabledAsync(bool value)
    {
        if (!Definition.Bundled || !CanToggle || value == IsEnabled) return;
        saving = true;
        OnPropertyChanged(nameof(CanToggle));
        try
        {
            if (Definition.WriteEnabled is { } write) await write(value);
            else await Task.Run(() => CheckpointSettings.SetEnabled(value));
            IsEnabled = value;
            Status = Definition.WriteEnabled is null
                ? value ? "Enabled · applies before the next request" : "Disabled · existing recovery data is kept"
                : Definition.CheckInstallation().Status;
        }
        catch (Exception error) { Status = "Could not save: " + error.Message; }
        finally
        {
            saving = false;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(CanToggle));
        }
    }
}
