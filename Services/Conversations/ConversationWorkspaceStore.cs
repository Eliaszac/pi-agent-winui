using PiAgentGui.Models.Projects;
using PiAgentGui.ViewModels.Conversations;

namespace PiAgentGui.Services.Conversations;

/// <summary>Owns one workspace per saved identity. Selection does not control process lifetime.</summary>
public sealed class ConversationWorkspaceStore(
    Func<Project, ConversationDraft, IConversationSession> sessionFactory, IUiDispatcher dispatcher) : IAsyncDisposable
{
    private readonly Dictionary<(Guid Project, Guid Conversation), ConversationViewModel> workspaces = [];
    private bool disposed;
    public event Action<Guid, Guid, string>? SessionNameChanged;
    public event Action<Guid, Guid, string>? ExplicitSessionNameChanged;

    public ConversationViewModel GetOrCreate(Project project, ConversationDraft conversation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = (project.Id, conversation.Id);
        if (!workspaces.TryGetValue(key, out var workspace))
        {
            workspace = new ConversationViewModel(sessionFactory(project, conversation), dispatcher);
            workspace.SessionNameChanged += name => SessionNameChanged?.Invoke(project.Id, conversation.Id, name);
            workspace.ExplicitSessionNameChanged += name => ExplicitSessionNameChanged?.Invoke(project.Id, conversation.Id, name);
            workspaces.Add(key, workspace);
        }
        return workspace;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        await Task.WhenAll(workspaces.Values.Select(workspace => workspace.DisposeAsync().AsTask()));
        workspaces.Clear();
    }

    public async Task RemoveAsync(Guid projectId, Guid? conversationId = null)
    {
        var removed = workspaces.Where(pair => pair.Key.Project == projectId && (conversationId is null || pair.Key.Conversation == conversationId)).ToArray();
        foreach (var pair in removed) workspaces.Remove(pair.Key);
        await Task.WhenAll(removed.Select(pair => pair.Value.DisposeAsync().AsTask()));
    }
}
