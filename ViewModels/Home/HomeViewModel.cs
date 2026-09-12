using System.ComponentModel;
using PiAgentGui.Models.Home;
using PiAgentGui.Services.Home;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Shell;

namespace PiAgentGui.ViewModels.Home;

public sealed class HomeViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel shell;
    private readonly SessionUsageReader reader;
    private CancellationTokenSource? read;
    private UsageInventory inventory = new([], 0, 0);
    private bool isLoading;
    private bool hasRead;
    private bool disposed;
    private int rangeIndex = 1;
    private string error = "";
    private HomeUsageSummary summary = HomeUsageAggregator.Summarize(new([], 0, 0), new Dictionary<Guid, string>(), DateOnly.FromDateTime(DateTime.Now), 30);
    public IReadOnlyList<HomeConversation> Recent { get; private set; } = [];
    public bool HasRecent => Recent.Count > 0;
    public bool HasNoRecent => !HasRecent;
    public string RecentEmptyDescription => shell.Projects.Count == 0
        ? "Add a project folder, then start a conversation. Your recent work will appear here."
        : "Start a new conversation in an existing project, or add another project folder.";
    public bool CanCreateConversation => shell.CanManageSidebar && shell.Projects.Count > 0;
    public bool CanManageProjects => shell.CanManageSidebar;
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public string Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public int RangeIndex { get => rangeIndex; set { if (SetProperty(ref rangeIndex, value is 0 ? 0 : 1)) ApplySummary(); } }
    public string TokenTotal => !hasRead || summary.Responses > 0 && summary.MissingUsage == summary.Responses ? "—" : summary.Tokens.ToString("N0");
    public string ResponseTotal => hasRead ? summary.Responses.ToString("N0") : "—";
    public string ProjectTotal => hasRead ? summary.ActiveProjects.ToString("N0") : "—";
    public IReadOnlyList<HomeUsageDay> Days => summary.Days;
    public IReadOnlyList<HomeUsageGroup> Models => summary.Models;
    public IReadOnlyList<HomeUsageGroup> Projects => summary.Projects;
    public bool HasUsage => hasRead && summary.Responses > 0;
    public bool ShowUsageEmpty => hasRead && summary.Responses == 0;
    public string Coverage => !hasRead ? "Reading local session records…" : summary.MissingUsage > 0 || inventory.UnavailableSessions > 0 || inventory.SkippedRecords > 0
        ? $"Partial history · {summary.MissingUsage:N0} responses without token counts · {inventory.UnavailableSessions:N0} unreadable or size-limited sessions · {inventory.SkippedRecords:N0} skipped records."
        : "Based on your saved conversations on this computer. Shared fork history is counted once.";
    public string PeriodStart => summary.Days[0].Date.ToString("MMM d");
    public string PeriodEnd => summary.Days[^1].Date.ToString("MMM d");
    public AsyncRelayCommand RefreshCommand { get; }

    public HomeViewModel(ShellViewModel shell, SessionUsageReader reader)
    {
        this.shell = shell;
        this.reader = reader;
        RefreshCommand = new(_ => RefreshAsync(), exception => Error = exception.Message);
        shell.PropertyChanged += OnShellChanged;
        RefreshRecent();
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (change.PropertyName == nameof(shell.ShowHome) && !shell.ShowHome) { read?.Cancel(); return; }
        if (change.PropertyName is nameof(shell.ShowHome) or nameof(shell.Projects) or nameof(shell.CanManageSidebar))
        {
            RefreshRecent();
            if (shell.ShowHome && shell.CanManageSidebar) _ = RefreshAsync();
        }
    }

    private void RefreshRecent()
    {
        var recent = shell.Projects.SelectMany(project => project.Conversations.Where(conversation => !conversation.IsSettled)
            .Select(conversation => (project, conversation)))
            .OrderByDescending(item => item.conversation.LastUsedAt).Take(6)
            .Select(item => new HomeConversation(item.conversation.Conversation.Id, item.project.Project.Id, item.conversation.Title, item.project.Name,
                item.conversation.LastUsedAt.LocalDateTime.ToString("MMM d · HH:mm"), new RelayCommand(_ =>
                {
                    shell.Projects.FirstOrDefault(project => project.Project.Id == item.project.Project.Id)?.Conversations
                        .FirstOrDefault(conversation => conversation.Conversation.Id == item.conversation.Conversation.Id)?.SelectCommand.Execute(null);
                }))).ToArray();
        if (!Recent.Select(row => (row.Id, row.ProjectId, row.Title, row.ProjectName, row.LastOpened))
            .SequenceEqual(recent.Select(row => (row.Id, row.ProjectId, row.Title, row.ProjectName, row.LastOpened))))
        { Recent = recent; OnPropertyChanged(nameof(Recent)); }
        foreach (var name in new[] { nameof(HasRecent), nameof(HasNoRecent), nameof(RecentEmptyDescription), nameof(CanCreateConversation), nameof(CanManageProjects) }) OnPropertyChanged(name);
    }

    public async Task RefreshAsync()
    {
        if (disposed || !shell.ShowHome || !shell.CanManageSidebar) return;
        read?.Cancel();
        RefreshRecent();
        IsLoading = true; Error = "";
        using var request = new CancellationTokenSource();
        read = request;
        try
        {
            var catalog = shell.Projects.Select(item => item.Project with { Conversations = item.Conversations.Select(conversation => conversation.Conversation).ToArray() }).ToArray();
            var next = await reader.ReadAsync(catalog, request.Token);
            request.Token.ThrowIfCancellationRequested();
            inventory = next; hasRead = true;
            ApplySummary();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception) { Error = "Couldn't read local usage. " + exception.Message; }
        finally { if (ReferenceEquals(read, request)) { read = null; IsLoading = false; } }
    }

    private void ApplySummary()
    {
        summary = HomeUsageAggregator.Summarize(inventory, shell.Projects.ToDictionary(project => project.Project.Id, project => project.Name),
            DateOnly.FromDateTime(DateTime.Now), RangeIndex == 0 ? 7 : 30);
        foreach (var name in new[] { nameof(TokenTotal), nameof(ResponseTotal), nameof(ProjectTotal), nameof(Days), nameof(Models), nameof(Projects),
            nameof(HasUsage), nameof(ShowUsageEmpty), nameof(Coverage), nameof(PeriodStart), nameof(PeriodEnd) }) OnPropertyChanged(name);
    }

    public void Dispose() { disposed = true; shell.PropertyChanged -= OnShellChanged; read?.Cancel(); }
}
