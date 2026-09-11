using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>Compares Pi's loaded file hashes with disk without changing runtime context.</summary>
public sealed class InstructionsPanelViewModel : ObservableObject
{
    private readonly InstructionFileStore store;
    private InstructionSnapshot? snapshot;
    private string? workingDirectory;
    private bool connected;
    private int revision;
    private IReadOnlyList<InstructionItem> items = [];
    private string message = "Open a conversation to inspect its instructions.";
    private string search = "";
    public InstructionsPanelViewModel(InstructionFileStore? store = null) => this.store = store ?? new();
    public IReadOnlyList<InstructionItem> Items => items.Where(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Scope.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
    public string Message { get => message; private set { if (SetProperty(ref message, value)) OnPropertyChanged(nameof(HasMessage)); } }
    public bool HasMessage => Message.Length > 0;
    public void Filter(string query) { search = query.Trim(); OnPropertyChanged(nameof(Items)); }

    public void Select(InstructionSnapshot? value, bool isConnected, string? directory)
    {
        revision++;
        snapshot = value; connected = isConnected; workingDirectory = directory;
        items = []; OnPropertyChanged(nameof(Items));
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var request = ++revision;
        if (!connected) { Message = "Conversation disconnected · loaded instructions unavailable."; return; }
        if (snapshot is null) { Message = "Refresh while Pi is idle to inspect loaded instructions, or start a run. Restart Pi Agent if this integration was just added."; return; }
        if (!snapshot.Available) { Message = "This Pi version cannot report its loaded instructions. Update Pi, then restart the app."; return; }
        if (snapshot.Files.Count == 0) { Message = "No instruction files were loaded. Add a global or project AGENTS.md and restart Pi Agent to load it."; return; }
        Message = "Checking saved files…";
        var directory = workingDirectory;
        var rows = await Task.WhenAll(snapshot.Files.Select(async file =>
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(file.Path));
            var scope = string.Equals(parent, Path.GetFullPath(PermissionModesSupport.AgentDirectory), StringComparison.OrdinalIgnoreCase)
                ? "Global" : string.Equals(parent, directory is null ? null : Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase)
                ? "Project" : "Project · inherited from " + Path.GetFileName(Path.GetDirectoryName(file.Path));
            try
            {
                var disk = await store.ReadAsync(file.Path);
                return new InstructionItem(file, scope, string.Equals(disk.ContentHash, file.Hash, StringComparison.OrdinalIgnoreCase)
                    ? "Loaded · matches disk" : "Disk differs · reload required", true);
            }
            catch (Exception) { return new InstructionItem(file, scope, "Loaded · file unavailable or not editable", false); }
        }));
        if (request != revision) return;
        items = rows;
        Message = "";
        OnPropertyChanged(nameof(Items));
    }

    public void ReportUnavailable() => Message = "Couldn't refresh loaded instructions. Wait until Pi is idle, then try Refresh again.";
}
