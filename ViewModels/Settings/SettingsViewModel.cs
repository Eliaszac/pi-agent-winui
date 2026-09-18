using PiAgentGui.Configuration;
using PiAgentGui.Models.Settings;
using PiAgentGui.Services.Settings;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class SettingsViewModel(AppSettingsStore store, Services.Home.SessionUsageReader? usage = null) : ObservableObject
{
    private string query = "";
    private string message = "";
    private bool busy;
    public AppUpdatesViewModel? AppUpdates { get; init; }
    public StorageOverviewService? StorageService { get; init; }
    public Func<Task<int>>? ClearCompletedResearch { get; init; }
    public Func<Task>? RestoreDefaultEditor { get; init; }
    private IReadOnlyList<Models.Settings.StorageUsage> storageItems = [];
    public IReadOnlyList<Models.Settings.StorageUsage> StorageItems { get => storageItems; private set => SetProperty(ref storageItems, value); }
    private bool scanning;
    public bool Scanning { get => scanning; private set { if (SetProperty(ref scanning, value)) OnPropertyChanged(nameof(CanRefreshStorage)); } }
    public bool CanRefreshStorage => !Scanning && !Busy;
    public async Task RefreshStorageAsync(bool reportSuccess = false)
    {
        if (Scanning || StorageService is null) return;
        Scanning = true;
        if (reportSuccess) SetFeedback("");
        try
        {
            StorageItems = await StorageService.ReadAsync();
            if (reportSuccess)
                SetFeedback(StorageItems.Any(item => item.Incomplete) ? "Storage sizes refreshed; some files could not be measured." : "Storage sizes refreshed.",
                    StorageItems.Any(item => item.Incomplete) ? SettingsFeedbackKind.Warning : SettingsFeedbackKind.Success);
        }
        catch (Exception error) { SetFeedback("Could not measure storage. " + error.Message, SettingsFeedbackKind.Error); }
        finally { Scanning = false; }
    }
    public SettingsCategory Storage { get; } = new("Storage",
        new("Overview", "stored on this PC disk space file sizes conversations artifacts sessions research checkpoints diagnostics screenshots"),
        new("Actions", "storage actions refresh sizes open app data folder manage checkpoint storage extensions retry pending cleanup clear diagnostics logs completed research files credentials"));
    public SettingsCategory Appearance { get; } = new("Appearance",
        new("Theme", "system light dark"),
        new("BodySize", "conversation text font size readability preview"),
        new("Weight", "response text font weight thickness regular medium semibold bold headings"),
        new("CodeSize", "code text font size preview"),
        new("Reset", "reset text sizes defaults"),
        new("Defaults", "restore reset appearance defaults"));
    public SettingsCategory Conversation { get; } = new("Conversation",
        new("SendKey", "send a message with enter ctrl control shortcut keyboard newline new line suggestions picker"),
        new("Sound", "completion sound audio cue mute viewed agent run finishes"),
        new("Defaults", "restore reset conversation defaults"));
    public SettingsCategory Terminal { get; } = new("Terminal",
        new("TextSize", "terminal text font size"),
        new("Scrollback", "scrollback lines history buffer Windows WSL SSH visible screen"),
        new("Defaults", "restore reset terminal settings defaults"));
    public SettingsCategory General { get; } = new("General",
        new("Startup", "on startup home resume last most recently opened conversation launch"),
        new("Editor", "preferred editor open in system default file association unavailable refresh editors"),
        new("Palette", "command palette reset command ranking counts recency usage search"),
        new("Defaults", "restore reset general defaults"));
    public SettingsCategory Usage { get; } = new("Local usage",
        new("Show", "show local usage on home analytics tokens models metadata retention privacy publisher"),
        new("Reset", "reset usage totals clear retained history statistics chats"),
        new("Defaults", "restore reset local usage defaults"));
    public SettingsCategory Data { get; } = new("Data management",
        new("Conversations", "delete all conversations screenshots messages restore data project folders shared Pi configuration"),
        new("Projects", "remove all projects registrations sidebar delete conversations files Windows WSL SSH folders"),
        new("Restore", "restore data other storage snapshots workspace checkpoints manage extensions research preferences diagnostics credentials remote cleanup"));
    public SettingsCategory About { get; } = new("About",
        new("Version", "version Pi desktop independent open source frontend publisher Eliaszac Denmark"),
        new("Legal", "legal privacy terms license licences notices"),
        new("Source", "source code GitHub open source"),
        new("Contact", "contact email eliaszacho@gmail.com"),
        new("Diagnostics", "preview copy diagnostic summary troubleshooting support system environment runtime version"));
    public SettingsCategory Updates { get; } = new("Updates",
        new("Status", "version update check download install restart release github status"),
        new("Checks", "check for updates automatically automatic background startup opens"),
        new("Downloads", "download updates automatically automatic install restart"),
        new("Defaults", "restore reset updates defaults"));
    public IReadOnlyList<SettingsCategory> Categories => [Appearance, Conversation, Terminal, General, Usage, Storage, Data, Updates, About];
    public double TerminalTextSize => store.Current.TerminalTextSize;
    public int TerminalScrollback => store.Current.TerminalScrollback;
    public bool CompletionAudio => store.Current.CompletionAudio;
    public Task SetTerminalSizeAsync(double size) => SaveAsync(store.Current with { TerminalTextSize = size });
    public Task SetTerminalScrollbackAsync(int lines) => SaveAsync(store.Current with { TerminalScrollback = lines });
    public Task SetCompletionAudioAsync(bool enabled) => SaveAsync(store.Current with { CompletionAudio = enabled });
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
    public string Message { get => message; private set { if (SetProperty(ref message, value)) OnPropertyChanged(nameof(HasMessage)); } }
    private SettingsFeedbackKind feedbackKind;
    public SettingsFeedbackKind FeedbackKind { get => feedbackKind; private set => SetProperty(ref feedbackKind, value); }
    public bool HasMessage => Message.Length > 0;
    public void SetFeedback(string text, SettingsFeedbackKind kind = SettingsFeedbackKind.Information)
    {
        FeedbackKind = kind;
        Message = text;
    }
    public int StartupIndex => store.Current.ResumeConversation ? 1 : 0;
    public bool ShowLocalUsage => store.Current.ShowLocalUsage;
    public string Version => ApplicationIdentity.Name + " · " + ApplicationIdentity.Version;
    public string CreateDiagnosticSummary() => SettingsDiagnosticSummary.Create(store.Current);
    public string UsageResetLabel => store.Current.UsageResetAt is { } reset ? "Showing usage since " + reset.LocalDateTime.ToString("g") : "Showing available saved history.";

    public async Task SetStartupAsync(int index) => await SaveAsync(store.Current with { ResumeConversation = index == 1 });
    public async Task SetUsageAsync(bool enabled) => await SaveAsync(store.Current with { ShowLocalUsage = enabled });
    public async Task ResetUsageAsync()
    {
        if (Busy) return;
        Busy = true; SetFeedback("");
        try
        {
            var cutoff = DateTimeOffset.UtcNow;
            await store.SaveAsync(store.Current with { UsageResetAt = cutoff });
            if (usage is not null) await usage.ResetAsync(cutoff);
            SetFeedback("Local usage totals have been reset. Your conversations were preserved.", SettingsFeedbackKind.Success);
        }
        catch (Exception error) { SetFeedback("Usage reset could not finish. " + error.Message, SettingsFeedbackKind.Error); }
        finally { Busy = false; OnPropertyChanged(nameof(UsageResetLabel)); }
    }
    public async Task RestoreDefaultsAsync(SettingsDefaultsCategory category)
    {
        if (Busy) return;
        if (AppUpdates?.SavingPreferences == true)
        {
            SetFeedback("Wait for update preferences to finish saving, then try again.", SettingsFeedbackKind.Warning);
            return;
        }
        Busy = true;
        SetFeedback("");
        var preferencesSaved = false;
        try
        {
            if (category == SettingsDefaultsCategory.General && RestoreDefaultEditor is null)
                throw new InvalidOperationException("The preferred editor setting is unavailable.");
            await store.SaveAsync(SettingsDefaults.Apply(store.Current, category));
            preferencesSaved = true;
            if (category == SettingsDefaultsCategory.General) await RestoreDefaultEditor!();
            SetFeedback(SettingsDefaults.Title(category) + " defaults restored. Other categories and saved data were preserved.", SettingsFeedbackKind.Success);
        }
        catch (Exception error)
        {
            SetFeedback(preferencesSaved
                ? "Startup defaults were saved, but the preferred editor could not be restored. Try restoring General defaults again. " + error.Message
                : "Defaults could not be restored. " + error.Message, SettingsFeedbackKind.Error);
        }
        finally
        {
            NotifyPreferencesChanged();
            AppUpdates?.RefreshPreferences();
            Busy = false;
        }
    }

    private async Task SaveAsync(Models.Settings.AppPreferences preferences)
    {
        if (Busy) return;
        Busy = true; SetFeedback("");
        try
        {
            await store.SaveAsync(preferences);
            SetFeedback("Settings saved.", SettingsFeedbackKind.Success);
        }
        catch (Exception error) { SetFeedback("Settings could not be saved. " + error.Message, SettingsFeedbackKind.Error); }
        finally
        {
            NotifyPreferencesChanged();
            Busy = false;
        }
    }

    private void NotifyPreferencesChanged()
    {
        OnPropertyChanged(nameof(StartupIndex)); OnPropertyChanged(nameof(ShowLocalUsage)); OnPropertyChanged(nameof(UsageResetLabel));
        OnPropertyChanged(nameof(ThemeIndex)); OnPropertyChanged(nameof(ConversationTextSize)); OnPropertyChanged(nameof(CodeTextSize)); OnPropertyChanged(nameof(SendKeyIndex));
        OnPropertyChanged(nameof(TextWeightIndex));
        OnPropertyChanged(nameof(TerminalTextSize)); OnPropertyChanged(nameof(TerminalScrollback)); OnPropertyChanged(nameof(CompletionAudio));
    }
}
