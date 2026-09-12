using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.ViewModels.Projects;

/// <summary>Represents a selectable conversation in the sidebar.</summary>
public sealed class ConversationItemViewModel : ObservableObject
{
    private bool isSelected;
    public ExecutionTarget? Target { get; }
    public string TargetGlyph => Target?.Glyph ?? "\uE7F4";
    public string TargetLabel => Target?.Label ?? "Local";
    public string TargetName => Target?.Name ?? "This computer";
    public string TargetLocation => Target is null || Target.IsLocal ? "Local Windows · This computer" : Target.Kind == "wsl" ? "WSL · " + Target.Host : "SSH · " + Target.Host;
    public string TargetPath => Target?.Path ?? "";
    public string PullRequestTitle => pullRequest?.Title ?? "";
    public string HoverDetails => $"{Title}\n\n{Target?.Description ?? "Local · This computer"}" + (pullRequest is null ? "" : $"\n\n{PullRequestLabel} · {pullRequest.Title}");
    public string AccessibleName => $"{Title}, {TargetLabel}";
    private Models.GitHub.GitHubPullRequest? pullRequest;
    public bool HasPullRequest => pullRequest is not null;
    public string PullRequestLabel => pullRequest is null ? "" : $"PR #{pullRequest.Number}";
    public string OpenPullRequestLabel => $"Open {PullRequestLabel}";
    internal Uri? PullRequestUrl => pullRequest?.Url;
    internal void SetPullRequest(Models.GitHub.GitHubPullRequest? value)
    {
        pullRequest = value;
        OnPropertyChanged(nameof(HasPullRequest));
        OnPropertyChanged(nameof(PullRequestLabel));
        OnPropertyChanged(nameof(PullRequestTitle));
        OnPropertyChanged(nameof(OpenPullRequestLabel));
        OnPropertyChanged(nameof(HoverDetails));
    }
    /// <summary>Gets the saved conversation.</summary>
    internal ConversationDraft Conversation { get; private set; }
    /// <summary>Gets the displayed title.</summary>
    public string Title => Conversation.Title;
    internal DateTimeOffset LastUsedAt => Conversation.LastUsedAt ?? Conversation.CreatedAt;
    internal void MarkUsed(DateTimeOffset usedAt) => Conversation = Conversation with { LastUsedAt = usedAt };
    /// <summary>Gets the conversation selection command.</summary>
    public RelayCommand SelectCommand { get; }
    public ConversationViewModel? Workspace { get; }
    public InlineRenameViewModel Rename { get; }
    public bool IsSettled => Conversation.IsSettled;
    public string SettleLabel => IsSettled ? "Restore conversation" : "Settle conversation";
    /// <summary>Gets or sets the selection highlight.</summary>
    public bool IsSelected
    {
        get => isSelected;
        set { if (SetProperty(ref isSelected, value)) Workspace?.SetViewed(value); }
    }

    /// <summary>Creates a sidebar entry.</summary>
    /// <param name="conversation">The saved draft.</param>
    /// <param name="select">The owning shell's selection action.</param>
    public ConversationItemViewModel(ConversationDraft conversation, Action<ConversationItemViewModel> select, ConversationViewModel? workspace = null,
        Func<ConversationItemViewModel, string, Task>? rename = null, ExecutionTarget? target = null)
    {
        Conversation = conversation;
        Target = target;
        Workspace = workspace;
        Rename = new InlineRenameViewModel(() => Title, name => rename?.Invoke(this, name) ?? Task.CompletedTask);
        SelectCommand = new RelayCommand(_ => select(this));
    }

    internal void SetTitle(string title, bool manual = true) { Conversation = Conversation with { Title = title, IsTitleManual = manual }; OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(HoverDetails)); OnPropertyChanged(nameof(AccessibleName)); }
    internal void SetSettled(bool settled)
    {
        Conversation = Conversation with { IsSettled = settled };
        OnPropertyChanged(nameof(IsSettled));
        OnPropertyChanged(nameof(SettleLabel));
    }
}
