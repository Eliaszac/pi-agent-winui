using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ChatEntryViewModel(ChatEntry entry) : ObservableObject
{
    public Services.Files.WorkspaceFileLinks? FileLinks { get; init; }
    public Func<string, string, string, SnippetViewModel>? SnippetFactory { get; init; }
    private ChatEntry entry = entry;
    private RunChangesViewModel? summary;
    public RunChangesViewModel? Summary
    {
        get => summary;
        internal set { if (SetProperty(ref summary, value)) OnPropertyChanged(nameof(HasSummary)); }
    }
    public bool HasSummary => Summary is { Files.Count: > 0 };
    public IReadOnlyList<ChatImage> Images => entry.Images ?? [];
    public bool HasImages => Images.Count > 0;
    private IReadOnlyList<ChatEntryViewModel> tools = [];
    private bool expanded;
    private bool showInlineActions = true;
    public bool ShowInlineActions { get => showInlineActions; internal set => SetProperty(ref showInlineActions, value); }
    public bool IsExpanded
    {
        get => expanded;
        set { if (SetProperty(ref expanded, value)) OnPropertyChanged(nameof(ExpansionGlyph)); }
    }
    public string ExpansionGlyph => expanded ? "\uE70D" : "\uE76C";
    public IReadOnlyList<ChatEntryViewModel> Tools => tools;
    public bool IsToolGroup => tools.Count > 1;
    private ArtifactItemViewModel? artifact;
    public ArtifactItemViewModel? Artifact
    {
        get => artifact;
        internal set
        {
            if (!SetProperty(ref artifact, value)) return;
            OnPropertyChanged(nameof(IsArtifact));
            OnPropertyChanged(nameof(IsTool));
            OnPropertyChanged(nameof(IsMessage));
            OnPropertyChanged(nameof(IsLeftAligned));
        }
    }
    public bool IsArtifact => Artifact is not null;
    public bool IsTool => entry.IsTool && !IsArtifact;
    public bool IsProcessing { get; init; }
    public bool IsMessage => !IsTool && !IsToolGroup && !IsProcessing && !IsArtifact;
    private string ToolDescription => entry.FileChange is { } change
        ? $"{Speaker} · {ProjectPathDisplay.ForTool(change.Path)}" + (change.Patch is not null ? $" · +{change.Added} −{change.Removed}" : $" · {Status}")
        : $"{Speaker} · {Status}";
    public string ToolSummary => ToolDescription + (entry.ToolTokens is long tokens ? $" · {tokens:N0} tokens" : "");
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
    internal ChatEntry Source => entry;
    public string Speaker => entry.Speaker;
    public string Text => entry.IsUser ? PromptFileReferences.Display(ArtifactPrompt.Display(entry.Text)) : entry.Text;
    private IReadOnlyList<ArtifactItemViewModel> attachedFiles = [];
    public IReadOnlyList<ArtifactItemViewModel> AttachedFiles
    {
        get => attachedFiles;
        internal set { if (SetProperty(ref attachedFiles, value)) OnPropertyChanged(nameof(HasAttachedFiles)); }
    }
    public bool HasAttachedFiles => AttachedFiles.Count > 0;
    public string Details => entry.Details;
    public string Status => entry.Status;
    public bool HasDetails => Details.Length > 0;
    public bool IsUser => entry.IsUser;
    internal bool IsAssistant => entry.IsAssistant;
    public bool ShowConversationActions { get; private set; }
    public bool CanDuplicate { get; private set; }
    public string UsageLabel { get; private set; } = "";
    public bool HasUsage => UsageLabel.Length > 0;
    internal void SetUsage(RunUsage usage)
    {
        UsageLabel = RunUsageFormatter.Format(usage);
        OnPropertyChanged(nameof(UsageLabel));
        OnPropertyChanged(nameof(HasUsage));
    }
    public AsyncRelayCommand? ForkCommand { get; internal set; }
    public AsyncRelayCommand? CloneCommand { get; internal set; }
    internal void SetConversationActions(bool visible, bool enabled)
    {
        if (ShowConversationActions != visible) { ShowConversationActions = visible; OnPropertyChanged(nameof(ShowConversationActions)); }
        if (CanDuplicate != enabled) { CanDuplicate = enabled; OnPropertyChanged(nameof(CanDuplicate)); }
    }
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
        OnPropertyChanged(nameof(Images));
        OnPropertyChanged(nameof(HasImages));
    }
}
