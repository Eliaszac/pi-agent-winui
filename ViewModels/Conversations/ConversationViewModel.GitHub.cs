using System.Collections.ObjectModel;
using PiAgentGui.Models.GitHub;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed partial class ConversationViewModel
{
    public ViewModels.GitHub.GitHubViewModel? GitHub { get; set; }
    public Func<CancellationToken, Task<string>>? ReadGitHubRepository { get; set; }
    public Task<string> GetGitHubRepositoryAsync(CancellationToken cancellation) => ReadGitHubRepository?.Invoke(cancellation)
        ?? (Target is { } target ? new Services.GitHub.ProjectGitHubRepository().ReadAsync(target, cancellation) : throw new IOException("No project repository is available."));
    public ObservableCollection<GitHubReference> PendingGitHub { get; } = [];
    public bool HasPendingGitHub => PendingGitHub.Count > 0;
    private bool preparingReferences;
    public bool PreparingReferences => preparingReferences;
    public void AttachGitHub(GitHubReference reference)
    {
        if (PendingGitHub.Any(item => item.Url == reference.Url)) return;
        if (PendingGitHub.Count >= 4) throw new IOException("Attach up to four GitHub references per message.");
        PendingGitHub.Add(reference with { Content = "" }); RefreshAttachments();
    }
    public void RemoveGitHub(GitHubReference reference) { PendingGitHub.Remove(reference); RefreshAttachments(); }
    private async Task<string> ExpandAttachmentsAsync(string text)
    {
        if (PendingFiles.Any(file => !file.Available)) throw new IOException("An attached artifact was deleted. Remove it before sending.");
        var files = PendingFiles.Select(file => file.Record).ToArray();
        var references = PendingGitHub.ToArray();
        var expanded = FileReferences.Expand(text);
        if (references.Length == 0) return ArtifactPrompt.Append(expanded, files);
        if (GitHub is null) throw new IOException("GitHub is unavailable. Connect it from Integrations.");
        preparingReferences = true; RefreshAttachments();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            var snapshots = new List<GitHubReference>();
            var repository = await GetGitHubRepositoryAsync(timeout.Token);
            if (references.Any(reference => !reference.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("An attached GitHub reference belongs to another repository. Remove it before sending. This project uses " + repository + ".");
            foreach (var reference in references)
            {
                var operation = GitHub.FetchReferenceAsync(reference, timeout.Token);
                TrackArtifactOperation(operation);
                snapshots.Add(await operation);
            }
            if (disposed) throw new IOException("This conversation was closed.");
            return ArtifactPrompt.Append(GitHubReferencePrompt.Append(expanded, snapshots), files);
        }
        catch (OperationCanceledException) { throw new IOException("GitHub context fetching timed out. Your draft has been kept; try again."); }
        finally { preparingReferences = false; RefreshAttachments(); }
    }
}
