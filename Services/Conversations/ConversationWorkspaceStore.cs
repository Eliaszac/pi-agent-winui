using PiAgentGui.Models.Projects;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Owns one workspace per saved identity. Selection does not control process lifetime.</summary>
public sealed class ConversationWorkspaceStore(
    Func<Project, ConversationDraft, IConversationSession> sessionFactory, IUiDispatcher dispatcher, bool previewCompacting = false,
    Pi.ModelFavoritesStore? modelFavorites = null) : IAsyncDisposable
{
    private readonly Dictionary<(Guid Project, Guid Conversation), ConversationViewModel> workspaces = [];
    private bool disposed;
    private readonly HashSet<ConversationViewModel> activeComputerWorkspaces = [];
    public int ActiveRunCount => workspaces.Values.Count(workspace => workspace.HasActiveWork);
    public bool HasActiveSnippet => workspaces.Values.Any(workspace => workspace.HasActiveSnippet);
    public event Action<Guid, Guid, string>? SessionNameChanged;
    public event Action<Guid, Guid, string>? ExplicitSessionNameChanged;
    public event Action? ViewedRunCompleted;
    public event Action? ComputerUseChanged;
    public Func<ExecutionTarget, Files.WorkspaceFileLinks>? FileLinkFactory { get; set; }
    public Func<Project, ConversationDraft, ArtifactPanelViewModel>? ArtifactFactory { get; set; }
    public Func<ConversationViewModel, bool, Task>? CopyRequested { get; set; }
    public void InvalidateProviderModels()
    {
        foreach (var workspace in workspaces.Values.ToArray()) workspace.InvalidateProviderModels();
    }

    public ConversationViewModel GetOrCreate(Project project, ConversationDraft conversation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = (project.Id, conversation.Id);
        if (!workspaces.TryGetValue(key, out var workspace))
        {
            workspace = new ConversationViewModel(sessionFactory(project, conversation), dispatcher, previewCompacting);
            if (modelFavorites is not null) workspace.ModelPicker = new ViewModels.Providers.ModelPickerViewModel(modelFavorites);
            workspace.Target = Utilities.ProjectTargets.Resolve(project, conversation.TargetId ?? project.Id);
            workspace.WorkingDirectory = workspace.Target.Path;
            workspace.FileLinks = FileLinkFactory?.Invoke(workspace.Target);
            workspace.Artifacts = ArtifactFactory?.Invoke(project, conversation);
            if (workspace.FileLinks is { } links) links.Artifacts = workspace.Artifacts?.Store;
            if (workspace.Artifacts is { } artifacts) artifacts.AttachToMessage = workspace.AttachArtifact;
            workspace.ResearchOwnerId = conversation.Id;
            workspace.ViewedRunCompleted += () => ViewedRunCompleted?.Invoke();
            workspace.SessionNameChanged += name => SessionNameChanged?.Invoke(project.Id, conversation.Id, name);
            workspace.ExplicitSessionNameChanged += name => ExplicitSessionNameChanged?.Invoke(project.Id, conversation.Id, name);
            workspace.DuplicateConversation = open => CopyRequested?.Invoke(workspace, open)
                ?? throw new InvalidOperationException("Conversation copying is unavailable.");
            workspaces.Add(key, workspace);
            workspace.PropertyChanged += OnWorkspacePropertyChanged;
        }
        return workspace;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        await Task.WhenAll(workspaces.Values.Select(workspace => workspace.DisposeAsync().AsTask()));
        workspaces.Clear();
        activeComputerWorkspaces.Clear();
        ComputerUseChanged?.Invoke();
    }

    public async Task RemoveAsync(Guid projectId, Guid? conversationId = null)
    {
        var removed = workspaces.Where(pair => pair.Key.Project == projectId && (conversationId is null || pair.Key.Conversation == conversationId)).ToArray();
        foreach (var pair in removed)
        {
            pair.Value.PropertyChanged -= OnWorkspacePropertyChanged;
            activeComputerWorkspaces.Remove(pair.Value);
            workspaces.Remove(pair.Key);
        }
        ComputerUseChanged?.Invoke();
        await Task.WhenAll(removed.Select(pair => pair.Value.DisposeAsync().AsTask()));
    }

    private void OnWorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ConversationViewModel.IsComputerUseActive) && sender is ConversationViewModel workspace)
        {
            var changed = workspace.IsComputerUseActive ? activeComputerWorkspaces.Add(workspace) : activeComputerWorkspaces.Remove(workspace);
            if (changed) ComputerUseChanged?.Invoke();
        }
    }
}
