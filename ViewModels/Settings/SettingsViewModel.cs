using PiAgentGui.Configuration;
using PiAgentGui.Services.Settings;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Settings;

public sealed class SettingsViewModel(AppSettingsStore store) : ObservableObject
{
    private string query = "";
    private string message = "";
    private bool busy;
    public SettingsCategory Appearance { get; } = new("Appearance", "theme system light dark font text code size readability reset");
    public SettingsCategory Conversation { get; } = new("Conversation", "send enter ctrl control shortcut keyboard newline");
    public SettingsCategory General { get; } = new("General", "startup home resume last conversation launch editor open in preferred system default command palette ranking reset usage");
    public SettingsCategory Usage { get; } = new("Local usage", "analytics tokens models reset clear history privacy");
    public SettingsCategory Data { get; } = new("Data management", "delete conversations projects screenshots restore checkpoints files storage");
    public SettingsCategory About { get; } = new("About", "version legal terms privacy license licences notices contact publisher open source");
    public IReadOnlyList<SettingsCategory> Categories => [Appearance, Conversation, General, Usage, Data, About];
    public int ThemeIndex => store.Current.Theme;
    public double ConversationTextSize => store.Current.ConversationTextSize;
    public double CodeTextSize => store.Current.CodeTextSize;
    public int SendKeyIndex => store.Current.ControlEnterToSend ? 1 : 0;
    public Task SetThemeAsync(int index) => SaveAsync(store.Current with { Theme = index });
    public Task SetTextSizeAsync(double size, bool code) => SaveAsync(code ? store.Current with { CodeTextSize = size } : store.Current with { ConversationTextSize = size });
    public Task ResetTextSizesAsync() => SaveAsync(store.Current with { ConversationTextSize = 15, CodeTextSize = 12 });
    public Task SetSendKeyAsync(int index) => SaveAsync(store.Current with { ControlEnterToSend = index == 1 });
    public string Query { get => query; set { if (!SetProperty(ref query, value)) return; foreach (var category in Categories) category.Filter(value); OnPropertyChanged(nameof(NoResults)); } }
    public bool NoResults => Categories.All(category => !category.Visible);
    public bool Busy { get => busy; set { if (SetProperty(ref busy, value)) OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => !Busy;
    public string Message { get => message; set { if (SetProperty(ref message, value)) OnPropertyChanged(nameof(HasMessage)); } }
    public bool HasMessage => Message.Length > 0;
    public int StartupIndex => store.Current.ResumeConversation ? 1 : 0;
    public bool ShowLocalUsage => store.Current.ShowLocalUsage;
    public string Version => ApplicationIdentity.Name + " · " + ApplicationIdentity.Version;
    public string UsageResetLabel => store.Current.UsageResetAt is { } reset ? "Showing usage since " + reset.LocalDateTime.ToString("g") : "Showing available saved history.";

    public async Task SetStartupAsync(int index) => await SaveAsync(store.Current with { ResumeConversation = index == 1 });
    public async Task SetUsageAsync(bool enabled) => await SaveAsync(store.Current with { ShowLocalUsage = enabled });
    public async Task ResetUsageAsync() => await SaveAsync(store.Current with { UsageResetAt = DateTimeOffset.UtcNow });
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
        }
    }
}
