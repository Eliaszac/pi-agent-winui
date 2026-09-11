using System.Collections.ObjectModel;
using PiAgentGui.Models.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.Services.Conversations;

namespace PiAgentGui.ViewModels.Projects;

/// <summary>Represents an expandable project and its conversation entries.</summary>
public sealed class ProjectItemViewModel : ObservableObject
{
    private bool isExpanded;
    private bool isSettledExpanded;
    /// <summary>Gets the saved project identity and metadata.</summary>
    internal Project Project { get; private set; }
    public InlineRenameViewModel Rename { get; }
    /// <summary>Gets the display name.</summary>
    public string Name => Project.Name;
    /// <summary>Gets the working directory.</summary>
    public string Path => Project.Path;
    public string RedactedPath => ProjectPathDisplay.Redact(Path);
    /// <summary>Gets the group expansion glyph.</summary>
    public string ExpansionGlyph => IsExpanded ? "\uE70D" : "\uE76C";
    /// <summary>Gets the accessible group action.</summary>
    public string ToggleLabel => $"{(IsExpanded ? "Collapse" : "Expand")} {Name}";
    /// <summary>Gets the accessible conversation creation action.</summary>
    public string NewConversationLabel => $"New conversation in {Name}";
    /// <summary>Gets or sets the group expansion state.</summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (!SetProperty(ref isExpanded, value)) return;
            OnPropertyChanged(nameof(ExpansionGlyph));
            OnPropertyChanged(nameof(ToggleLabel));
        }
    }
    /// <summary>Gets the group's conversation list.</summary>
    public ObservableCollection<ConversationItemViewModel> Conversations { get; } = [];
    public ObservableCollection<ConversationItemViewModel> ActiveConversations { get; } = [];
    public ObservableCollection<ConversationItemViewModel> SettledConversations { get; } = [];
    public string SettledLabel => $"Settled — ({SettledConversations.Count})";
    public bool HasSettledConversations => SettledConversations.Count > 0;
    public bool IsSettledExpanded { get => isSettledExpanded; set { if (SetProperty(ref isSettledExpanded, value)) OnPropertyChanged(nameof(SettledGlyph)); } }
    public string SettledGlyph => IsSettledExpanded ? "\uE70D" : "\uE76C";
    public RelayCommand ToggleSettledCommand { get; }
    /// <summary>Gets whether the group has no conversations.</summary>
    public bool HasNoConversations => Conversations.Count == 0;
    /// <summary>Gets the group expansion command, which does not change navigation.</summary>
    public RelayCommand ToggleCommand { get; }
    /// <summary>Gets the shared conversation creation command.</summary>
    public AsyncRelayCommand NewConversationCommand { get; }

    /// <summary>Creates a project group.</summary>
    /// <param name="project">The saved project.</param>
    /// <param name="selectConversation">The conversation selection action.</param>
    /// <param name="newConversationCommand">The shared creation command.</param>
    public ProjectItemViewModel(Project project,
        Action<ProjectItemViewModel, ConversationItemViewModel> selectConversation, AsyncRelayCommand newConversationCommand,
        ConversationWorkspaceStore? workspaces = null, Func<ProjectItemViewModel, string, Task>? renameProject = null,
        Func<ProjectItemViewModel, ConversationItemViewModel, string, Task>? renameConversation = null)
    {
        Project = project;
        Rename = new InlineRenameViewModel(() => Name, name => renameProject?.Invoke(this, name) ?? Task.CompletedTask);
        ToggleSettledCommand = new RelayCommand(_ => IsSettledExpanded = !IsSettledExpanded);
        NewConversationCommand = newConversationCommand;
        ToggleCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
        foreach (var conversation in project.Conversations)
            Conversations.Add(new ConversationItemViewModel(conversation, item => selectConversation(this, item), workspaces?.GetOrCreate(project, conversation),
                (item, name) => renameConversation?.Invoke(this, item, name) ?? Task.CompletedTask));
        Conversations.CollectionChanged += (_, _) => RefreshGroups();
        RefreshGroups();
    }

    internal void SetName(string name)
    {
        Project = Project with { Name = name };
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(NewConversationLabel));
    }

    internal void RefreshGroups()
    {
        ObservableCollectionSynchronizer.Synchronize(ActiveConversations, Conversations.Where(item => !item.IsSettled).OrderByDescending(item => item.LastUsedAt).ToArray());
        ObservableCollectionSynchronizer.Synchronize(SettledConversations, Conversations.Where(item => item.IsSettled).OrderByDescending(item => item.LastUsedAt).ToArray());
        OnPropertyChanged(nameof(HasNoConversations));
        OnPropertyChanged(nameof(HasSettledConversations));
        OnPropertyChanged(nameof(SettledLabel));
    }
}
