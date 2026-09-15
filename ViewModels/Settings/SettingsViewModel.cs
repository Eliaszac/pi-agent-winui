using PiAgentGui.Configuration;
using PiAgentGui.Services.Settings;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class SettingsViewModel(AppSettingsStore store, Services.Home.SessionUsageReader? usage = null) : ObservableObject
{
    private string query = "";
    private string message = "";
    private bool busy;
    public StorageOverviewService? StorageService { get; init; }
    public Func<Task<int>>? ClearCompletedResearch { get; init; }
    private IReadOnlyList<Models.Settings.StorageUsage> storageItems = [];
    public IReadOnlyList<Models.Settings.StorageUsage> StorageItems { get => storageItems; private set => SetProperty(ref storageItems, value); }
    private bool scanning;
    public bool Scanning { get => scanning; private set { if (SetProperty(ref scanning, value)) OnPropertyChanged(nameof(CanRefreshStorage)); } }
    public bool CanRefreshStorage => !Scanning && !Busy;
    public async Task RefreshStorageAsync()
    {
        if (Scanning || StorageService is null) return;
        Scanning = true;
        try { StorageItems = await StorageService.ReadAsync(); }
        catch (Exception error) { Message = "Could not measure storage. " + error.Message; }
        finally { Scanning = false; }
    }
    public SettingsCategory Storage { get; } = new("Storage", "disk space size artifacts sessions research checkpoints diagnostics logs cleanup folder retry");
    public SettingsCategory Appearance { get; } = new("Appearance", "theme system light dark font text code size weight thickness regular medium semibold readability reset");
    public SettingsCategory Conversation { get; } = new("Conversation", "send enter ctrl control shortcut keyboard newline");
    public SettingsCategory General { get; } = new("General", "startup home resume last conversation launch editor open in preferred system default command palette ranking reset usage");
    public SettingsCategory Usage { get; } = new("Local usage", "analytics tokens models reset clear history privacy");
    public SettingsCategory Data { get; } = new("Data management", "delete conversations projects screenshots restore checkpoints files storage");
    public SettingsCategory About { get; } = new("About", "version legal terms privacy license licences notices contact publisher open source");
    public IReadOnlyList<SettingsCategory> Categories => [Appearance, Conversation, General, Usage, Storage, Data, About];
    public int ThemeIndex => store.Current.Theme;
    public double ConversationTextSize => store.Current.ConversationTextSize;
    public double CodeTextSize => store.Current.CodeTextSize;
    public int TextWeightIndex => (store.Current.ConversationTextWeight - 400) / 100;
    public Task SetTextWeightAsync(int index) => SaveAsync(store.Current with { ConversationTextWeight = 400 + Math.Clamp(index, 0, 2) * 100 });
    public int SendKeyIndex => store.Current.ControlEnterToSend ? 1 : 0;
    public Task SetThemeAsync(int index) => SaveAsync(store.Current with { Theme = index });
    public Task SetTextSizeAsync(double size, bool code) => SaveAsync(code ? store.Current with { CodeTextSize = size } : store.Current with { ConversationTextSize = size });
    public Task ResetTextSizesAsync() => SaveAsync(store.Current with { ConversationTextSize = 15, CodeTextSize = 12 });
    public Task SetSendKeyAsync(int index) => SaveAsync(store.Current with { ControlEnterToSend = index == 1 });
    public string Query { get => query; set { if (!SetProperty(ref query, value)) return; foreach (var category in Categories) category.Filter(value); OnPropertyChanged(nameof(NoResults)); } }
    public bool NoResults => Categories.All(category => !category.Visible);
    public bool Busy { get => busy; set { if (SetProperty(ref busy, value)) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanRefreshStorage)); } } }
    public bool CanEdit => !Busy;
    public string Message { get => message; set { if (SetProperty(ref message, value)) OnPropertyChanged(nameof(HasMessage)); } }
    public bool HasMessage => Message.Length > 0;
    public int StartupIndex => store.Current.ResumeConversation ? 1 : 0;
    public bool ShowLocalUsage => store.Current.ShowLocalUsage;
    public string Version => ApplicationIdentity.Name + " · " + ApplicationIdentity.Version;
    public string UsageResetLabel => store.Current.UsageResetAt is { } reset ? "Showing usage since " + reset.LocalDateTime.ToString("g") : "Showing available saved history.";

    public async Task SetStartupAsync(int index) => await SaveAsync(store.Current with { ResumeConversation = index == 1 });
    public async Task SetUsageAsync(bool enabled) => await SaveAsync(store.Current with { ShowLocalUsage = enabled });
    public async Task ResetUsageAsync()
    {
        if (Busy) return;
        Busy = true; Message = "";
        try
        {
            var cutoff = DateTimeOffset.UtcNow;
            await store.SaveAsync(store.Current with { UsageResetAt = cutoff });
            if (usage is not null) await usage.ResetAsync(cutoff);
        }
        catch (Exception error) { Message = "Usage reset could not finish. " + error.Message; }
        finally { Busy = false; OnPropertyChanged(nameof(UsageResetLabel)); }
    }
    private async Task SaveAsync(Models.Settings.AppPreferences preferences)
    {
        if (Busy) return;
        Busy = true; Message = "";
        try { await store.SaveAsync(preferences); }
        catch (Exception error) { Message = "Settings could not be saved. " + error.Message; }
        finally
        {
            Busy = false;
            OnPropertyChanged(nameof(StartupIndex)); OnPropertyChanged(nameof(ShowLocalUsage)); OnPropertyChanged(nameof(UsageResetLabel));
            OnPropertyChanged(nameof(ThemeIndex)); OnPropertyChanged(nameof(ConversationTextSize)); OnPropertyChanged(nameof(CodeTextSize)); OnPropertyChanged(nameof(SendKeyIndex));
            OnPropertyChanged(nameof(TextWeightIndex));
        }
    }
}
