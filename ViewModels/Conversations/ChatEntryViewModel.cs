using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ChatEntryViewModel(ChatEntry entry) : ObservableObject
{
    private ChatEntry entry = entry;
    private IReadOnlyList<ChatEntryViewModel> tools = [];
    private bool expanded;
    public bool IsExpanded
    {
        get => expanded;
        set { if (SetProperty(ref expanded, value)) OnPropertyChanged(nameof(ExpansionGlyph)); }
    }
    public string ExpansionGlyph => expanded ? "\uE70D" : "\uE76C";
    public IReadOnlyList<ChatEntryViewModel> Tools => tools;
    public bool IsToolGroup => tools.Count > 3;
    public bool IsTool => entry.IsTool;
    public bool IsProcessing { get; init; }
    public bool IsMessage => !IsTool && !IsToolGroup && !IsProcessing;
    public string ToolSummary => entry.FileChange is { } change
        ? $"{Speaker} · {ProjectPathDisplay.ForTool(change.Path)}" + (change.Patch is not null ? $" · +{change.Added} −{change.Removed}" : $" · {Status}")
        : $"{Speaker} · {Status}";
    public string? DiffPatch => entry.FileChange?.Patch;
    internal FileChange? FileChange => entry.FileChange;
    public bool HasDiff => DiffPatch is not null;
    public bool ShowRawToolDetails => !HasDiff;
    public string DiffNotice => entry.FileChange?.Unavailable ?? "";
    public bool HasDiffNotice => DiffNotice.Length > 0;
    public string GroupSummary => $"Called {tools.Count} tools" + (tools.Any(tool => tool.Status == "Running") ? " · running" : tools.Any(tool => tool.Status.StartsWith("Failed", StringComparison.Ordinal)) ? " · includes failures" : "");
    internal void SetTools(IReadOnlyList<ChatEntryViewModel> next)
    {
        if (!tools.SequenceEqual(next)) { tools = next; OnPropertyChanged(nameof(Tools)); }
        OnPropertyChanged(nameof(IsToolGroup));
        OnPropertyChanged(nameof(IsMessage));
        OnPropertyChanged(nameof(GroupSummary));
    }
    internal string Id => entry.Id;
    public string Speaker => entry.Speaker;
    public string Text => entry.Text;
    public string Details => entry.Details;
    public string Status => entry.Status;
    public bool HasDetails => Details.Length > 0;
    public bool IsUser => entry.IsUser;
    public bool IsLeftAligned => !IsUser && IsMessage;
    public bool IsEmptyAssistant => entry.IsAssistant && string.IsNullOrWhiteSpace(Text) && !Status.StartsWith("Failed", StringComparison.Ordinal);
    public bool ShowSpeaker => !entry.IsUser && !entry.IsAssistant;
    public bool HasHeader => ShowSpeaker || (Status.Length > 0 && Status != "Responding");
    public bool CanCopyUser => IsUser && Text.Length > 0;
    public bool CanCopyResponse => entry.IsAssistant && entry.IsComplete && Text.Length > 0;

    internal void Update(ChatEntry next)
    {
        entry = next;
        OnPropertyChanged(nameof(Speaker));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(IsUser));
        OnPropertyChanged(nameof(IsLeftAligned));
        OnPropertyChanged(nameof(ShowSpeaker));
        OnPropertyChanged(nameof(HasHeader));
        OnPropertyChanged(nameof(ToolSummary));
        OnPropertyChanged(nameof(DiffPatch));
        OnPropertyChanged(nameof(HasDiff));
        OnPropertyChanged(nameof(ShowRawToolDetails));
        OnPropertyChanged(nameof(DiffNotice));
        OnPropertyChanged(nameof(HasDiffNotice));
        OnPropertyChanged(nameof(CanCopyUser));
        OnPropertyChanged(nameof(CanCopyResponse));
    }
}
