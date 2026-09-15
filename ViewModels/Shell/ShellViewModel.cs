using System.Collections.ObjectModel;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Projects;
using PiAgentGui.Services.Conversations;
using PiAgentGui.ViewModels.Conversations;
using PiAgentGui.Configuration;

namespace PiAgentGui.ViewModels.Shell;

/// <summary>Coordinates the saved project list and current workspace selection.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly IProjectRepository repository;
    private readonly ConversationWorkspaceStore? workspaces;
    private readonly PiSessionPaths sessionPaths;
    private readonly ConversationDataCleanup? dataCleanup;
    private readonly SemaphoreSlim titleGate = new(1, 1);
    private bool isLoading;
    private bool isReady;
    private bool hasLoaded;
    private bool isChanging;
    public bool CanManageSidebar => IsReady && !isChanging;
    private ObservableCollection<ProjectItemViewModel> projects = [];
    private string errorMessage = "";
    private ProjectItemViewModel? selectedProject;
    private ConversationItemViewModel? selectedConversation;
    private bool showExtensions;
    private bool showProviders;
    private bool showHome = true;
    private bool showSettings;
    private bool showLegal;
    private bool showIntegrations;
    public bool ShowIntegrations => showIntegrations;
    public void OpenIntegrations()
    {
        OpenSettings();
        showSettings = false; showIntegrations = true;
        OnPropertyChanged(nameof(ShowSettings)); OnPropertyChanged(nameof(ShowIntegrations)); OnPropertyChanged(nameof(ShowWorkspace));
    }
    public bool ShowSettings => showSettings;
    public bool ShowLegal => showLegal;
    public bool ResumeConversationOnStartup { get; set; }
    public bool HasActiveWork => workspaces?.ActiveRunCount > 0 || workspaces?.HasActiveSnippet == true;
    private void CloseSettingsPages()
    {
        showSettings = showLegal = showIntegrations = false;
        OnPropertyChanged(nameof(ShowIntegrations));
        OnPropertyChanged(nameof(ShowSettings)); OnPropertyChanged(nameof(ShowLegal));
    }
    public void OpenSettings(bool legal = false)
    {
        showIntegrations = false; OnPropertyChanged(nameof(ShowIntegrations));
        showHome = showProviders = showExtensions = false;
        showLegal = legal; showSettings = !legal;
        Chat?.SetViewed(false);
        foreach (var name in new[] { nameof(ShowHome), nameof(ShowProviders), nameof(ShowExtensions), nameof(ShowSettings), nameof(ShowLegal), nameof(ShowWorkspace) }) OnPropertyChanged(name);
        if (Sidebar.IsOverlay) Sidebar.IsOpen = false;
    }
    public bool ShowHome => showHome;
    public bool ShowProviders => showProviders;
    public ViewModels.Providers.ProvidersViewModel? Providers { get; set; }
    public bool ShowExtensions => showExtensions;
    public bool ShowWorkspace => !showExtensions && !showProviders && !showHome && !showSettings && !showLegal && !showIntegrations;
    public ViewModels.Extensions.ExtensionsViewModel Extensions { get; }
    public void OpenExtensions()
    {
        CloseSettingsPages();
        showHome = false;
        OnPropertyChanged(nameof(ShowHome));
        showProviders = false;
        OnPropertyChanged(nameof(ShowProviders));
        showExtensions = true;
        Chat?.SetViewed(false);
        OnPropertyChanged(nameof(ShowExtensions));
        OnPropertyChanged(nameof(ShowWorkspace));
        _ = Extensions.RefreshCommand.ExecuteAsync();
        if (Sidebar.IsOverlay) Sidebar.IsOpen = false;
    }
    public void CloseExtensions()
    {
        CloseSettingsPages();
        showHome = false;
        OnPropertyChanged(nameof(ShowHome));
        showProviders = false;
        OnPropertyChanged(nameof(ShowProviders));
        showExtensions = false;
        Chat?.SetViewed(true);
        OnPropertyChanged(nameof(ShowExtensions));
        OnPropertyChanged(nameof(ShowWorkspace));
    }

    public void OpenProviders()
    {
        CloseSettingsPages();
        showHome = false;
        OnPropertyChanged(nameof(ShowHome));
        showExtensions = false;
        showProviders = true;
        Chat?.SetViewed(false);
        OnPropertyChanged(nameof(ShowProviders));
        OnPropertyChanged(nameof(ShowExtensions));
        OnPropertyChanged(nameof(ShowWorkspace));
        if (Providers is not null) _ = Providers.RefreshAsync();
        if (Sidebar.IsOverlay) Sidebar.IsOpen = false;
    }

    public void OpenHome()
    {
        CloseSettingsPages();
        showExtensions = false;
        showProviders = false;
        showHome = true;
        Chat?.SetViewed(false);
        OnPropertyChanged(nameof(ShowExtensions));
        OnPropertyChanged(nameof(ShowProviders));
        OnPropertyChanged(nameof(ShowHome));
        OnPropertyChanged(nameof(ShowWorkspace));
        if (Sidebar.IsOverlay) Sidebar.IsOpen = false;
    }

    /// <summary>Gets the project groups.</summary>
    public ObservableCollection<ProjectItemViewModel> Projects => projects;
    /// <summary>Gets whether a resolved workspace can be displayed, including during reload.</summary>
    public bool HasLoaded => hasLoaded;
    /// <summary>Gets whether the first workspace load is pending.</summary>
    public bool ShowInitialLoading => !HasLoaded && IsLoading;
    /// <summary>Gets the sidebar layout state.</summary>
    public SidebarLayoutState Sidebar { get; } = new();
    /// <summary>Gets the reload action.</summary>
    public AsyncRelayCommand ReloadCommand { get; }
    /// <summary>Gets the shared new conversation action.</summary>
    public AsyncRelayCommand NewConversationCommand { get; }
    /// <summary>Gets whether the catalog has been loaded successfully.</summary>
    public bool IsReady { get => isReady; private set { if (SetProperty(ref isReady, value)) OnPropertyChanged(nameof(CanManageSidebar)); } }
    /// <summary>Gets whether the catalog is loading.</summary>
    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (!SetProperty(ref isLoading, value)) return;
            OnPropertyChanged(nameof(ShowEmptyProjects));
            OnPropertyChanged(nameof(ShowInitialLoading));
        }
    }
    /// <summary>Gets whether the initial project hint is visible.</summary>
    public bool ShowEmptyProjects => HasLoaded && !IsLoading && Projects.Count == 0 && !HasError;
    /// <summary>Gets the latest operation error.</summary>
    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (!SetProperty(ref errorMessage, value)) return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyProjects));
        }
    }
    /// <summary>Gets whether operation feedback is visible.</summary>
    public bool HasError => ErrorMessage.Length > 0;
    /// <summary>Gets the currently selected project.</summary>
    public ProjectItemViewModel? SelectedProject => selectedProject;
    public ConversationItemViewModel? SelectedConversation => selectedConversation;
    public ExecutionTarget? SelectedTarget => selectedConversation?.Target ?? selectedProject?.DefaultTarget;
    public string TargetLabel => SelectedTarget?.Label ?? "";
    public string? LocalWorkspacePath => SelectedTarget is { IsLocal: true } target ? target.Path : null;
    public Func<ProjectItemViewModel, Task<Guid?>>? ChooseTargetAsync { get; set; }
    public async Task RefreshTargetsAsync(ProjectItemViewModel project)
    {
        var saved = (await repository.GetAllAsync()).Single(p => p.Id == project.Project.Id);
        project.SetTargets(saved); NotifySelection();
    }
    public InlineRenameViewModel HeaderRename { get; private set; } = new(() => "", _ => Task.CompletedTask);
    /// <summary>Gets the workspace header.</summary>
    public string WorkspaceTitle => selectedConversation?.Title ?? selectedProject?.Name ?? "Your workspace";
    /// <summary>Gets the native window title for the selected conversation.</summary>
    public string WindowTitle => selectedConversation is null ? ApplicationIdentity.Name : $"{ApplicationIdentity.Name} — {selectedConversation.Title}";
    /// <summary>Gets the selected working directory.</summary>
    public string WorkspacePath => SelectedTarget?.Path ?? "Projects and conversations, in one place.";
    /// <summary>Gets the main empty-state heading.</summary>
    public string WelcomeTitle => selectedProject?.Name ?? "Make room for your next idea.";
    public IReadOnlyList<WelcomeConversation> RecentConversations => Projects
        .Where(project => selectedProject is null || ReferenceEquals(project, selectedProject))
        .SelectMany(project => project.Conversations.Where(conversation => !conversation.IsSettled)
            .Select(conversation => new WelcomeConversation(project.Name, conversation)))
        .OrderByDescending(item => item.Conversation.LastUsedAt).Take(4).ToArray();
    public bool HasRecentConversations => RecentConversations.Count > 0;
    /// <summary>Gets context-sensitive workspace guidance.</summary>
    public string WelcomeDescription => selectedConversation is not null
        ? "Send a message to work with Pi in this project's folder."
        : selectedProject is not null ? "Pick up where you left off, or give Pi something new to work on."
        : "Choose a folder. Start a conversation. Keep the work together.";
    /// <summary>Gets whether the welcome action creates a project.</summary>
    public bool ShowCreateProject => selectedProject is null;
    /// <summary>Gets whether the welcome action creates a conversation.</summary>
    public bool ShowCreateConversation => selectedProject is not null && selectedConversation is null;
    /// <summary>Gets whether a saved draft is selected.</summary>
    public bool HasConversation => selectedConversation is not null;
    public ConversationViewModel? Chat => selectedConversation?.Workspace;
    public ConversationItemViewModel? ComputerUseConversation => Projects.SelectMany(project => project.Conversations)
        .FirstOrDefault(conversation => conversation.Workspace?.IsComputerUseActive == true);
    public bool HasComputerUse => ComputerUseConversation is not null;
    public bool ShowWelcome => HasLoaded && !HasConversation;

    /// <summary>Creates the shell without performing disk I/O.</summary>
    /// <param name="repository">The shared project persistence boundary.</param>
    public ShellViewModel(IProjectRepository repository, ConversationWorkspaceStore? workspaces = null, PiSessionPaths? sessionPaths = null, ConversationDataCleanup? dataCleanup = null,
        ViewModels.Extensions.ExtensionsViewModel? extensions = null)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Extensions = extensions ?? new();
        this.workspaces = workspaces;
        if (workspaces is not null) workspaces.ComputerUseChanged += () =>
        {
            OnPropertyChanged(nameof(ComputerUseConversation));
            OnPropertyChanged(nameof(HasComputerUse));
        };
        this.sessionPaths = sessionPaths ?? new(new ProjectStorageOptions());
        this.dataCleanup = dataCleanup;
        if (workspaces is not null) workspaces.CopyRequested = DuplicateConversationAsync;
        if (workspaces is not null) workspaces.SessionNameChanged += OnSessionNameChanged;
        if (workspaces is not null) workspaces.ExplicitSessionNameChanged += OnExplicitSessionNameChanged;
        ReloadCommand = new AsyncRelayCommand(_ => LoadAsync(), ReportError);
        NewConversationCommand = new AsyncRelayCommand(CreateConversationAsync, ReportError);
    }

    /// <summary>Loads the catalog and preserves matching selections and expansion states.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading || isChanging) return;
        IsLoading = true;
        IsReady = false;
        ErrorMessage = "";
        try
        {
            var saved = await Task.Run(() => repository.GetAllAsync());
            var expanded = Projects.ToDictionary(project => project.Project.Id, project => project.IsExpanded);
            var settledExpanded = Projects.ToDictionary(project => project.Project.Id, project => project.IsSettledExpanded);
            var projectId = selectedProject?.Project.Id;
            var keepHome = ShowHome;
            var keepSettings = ShowSettings;
            var keepLegal = ShowLegal;
            var keepIntegrations = ShowIntegrations;
            var conversationId = selectedConversation?.Conversation.Id;
            var loadedProjects = new ObservableCollection<ProjectItemViewModel>();
            foreach (var project in saved.Reverse())
            {
                var item = CreateProjectItem(project);
                item.IsExpanded = expanded.TryGetValue(project.Id, out var wasExpanded) ? wasExpanded : loadedProjects.Count == 0;
                item.IsSettledExpanded = settledExpanded.GetValueOrDefault(project.Id);
                loadedProjects.Add(item);
            }
            // Publish a complete list and final selection rather than clear/add and project/conversation stages.
            projects = loadedProjects;
            var selected = Projects.FirstOrDefault(project => project.Project.Id == projectId) ?? Projects.FirstOrDefault();
            var conversation = selected?.Conversations.FirstOrDefault(item => item.Conversation.Id == conversationId);
            if (!hasLoaded && ResumeConversationOnStartup)
            {
                var recent = Projects.SelectMany(project => project.Conversations.Select(item => (project, item)))
                    .OrderByDescending(pair => pair.item.LastUsedAt).FirstOrDefault();
                if (recent.item is not null) { selected = recent.project; conversation = recent.item; keepHome = false; }
            }
            SetSelection(selected, conversation);
            if (keepHome) OpenHome();
            else if (keepIntegrations) OpenIntegrations();
            else if (keepSettings || keepLegal) OpenSettings(keepLegal);
            OnPropertyChanged(nameof(Projects));
            hasLoaded = true;
            OnPropertyChanged(nameof(HasLoaded));
            OnPropertyChanged(nameof(ShowWelcome));
            OnPropertyChanged(nameof(ShowInitialLoading));
            IsReady = true;
            _ = CleanupDeletedDataAsync();
        }
        catch (Exception exception) { ReportError(exception); }
        finally { IsLoading = false; }
    }

    /// <summary>Adds a successfully persisted project to the sidebar.</summary>
    /// <param name="project">The saved project.</param>
    public void AddSavedProject(Project project)
    {
        var item = CreateProjectItem(project);
        item.IsExpanded = true;
        Projects.Insert(0, item);
        SelectProject(item);
        // Keep narrow workspaces visible rather than opening an overlay above the new selection.
        Sidebar.IsOpen = !Sidebar.IsOverlay;
        ErrorMessage = "";
        OnPropertyChanged(nameof(ShowEmptyProjects));
    }

    /// <summary>Displays an operation failure.</summary>
    /// <param name="exception">The failure to describe.</param>
    public void ReportError(Exception exception) => ErrorMessage = ProjectErrorMessage.From(exception);

    private ProjectItemViewModel CreateProjectItem(Project project) =>
        new(project, SelectConversation, NewConversationCommand, workspaces, RenameProjectAsync, RenameConversationAsync);

    public void SelectProject(ProjectItemViewModel? project)
    {
        var fromHome = ShowHome;
        CloseExtensions();
        if (!fromHome && ReferenceEquals(selectedProject, project) && selectedConversation is not null) return;
        SetSelection(project, null);
    }

    private void SelectConversation(ProjectItemViewModel project, ConversationItemViewModel conversation)
    {
        var usedAt = DateTimeOffset.UtcNow;
        conversation.MarkUsed(usedAt);
        project.RefreshGroups();
        _ = PersistConversationUsageAsync(project.Project.Id, conversation.Conversation.Id, usedAt);
        SetSelection(project, conversation);
        if (Sidebar.IsOverlay) Sidebar.IsOpen = false;
    }

    private readonly SemaphoreSlim usageWrites = new(1, 1);

    private async Task PersistConversationUsageAsync(Guid projectId, Guid conversationId, DateTimeOffset usedAt)
    {
        await usageWrites.WaitAsync();
        try { await repository.TouchConversationAsync(projectId, conversationId, usedAt); }
        catch (Exception exception) { ReportError(exception); }
        finally { usageWrites.Release(); }
    }

    private void SetSelection(ProjectItemViewModel? project, ConversationItemViewModel? conversation)
    {
        CloseExtensions();
        if (selectedConversation is not null) selectedConversation.IsSelected = false;
        selectedProject = project;
        selectedConversation = conversation;
        HeaderRename.CancelCommand.Execute(null);
        HeaderRename = new(() => conversation?.Title ?? "", title => project is not null && conversation is not null
            ? RenameConversationAsync(project, conversation, title) : Task.CompletedTask);
        OnPropertyChanged(nameof(HeaderRename));
        if (conversation is not null) conversation.IsSelected = true;
        NotifySelection();
        _ = Chat?.InitializeAsync();
    }

    private Task CreateConversationAsync(object? parameter)
    {
        if (!CanManageSidebar || parameter is not ProjectItemViewModel project) return Task.CompletedTask;
        return ChangeSidebarAsync(async () =>
        {
            var targetId = project.Targets.Count == 1 ? project.Targets[0].Id
                : ChooseTargetAsync is null ? project.DefaultTarget.Id : await ChooseTargetAsync(project);
            if (targetId is null) return;
            var saved = await Task.Run(() => repository.AddConversationAsync(project.Project.Id, targetId: targetId));
            var item = new ConversationItemViewModel(saved, conversation => SelectConversation(project, conversation), workspaces?.GetOrCreate(project.Project, saved),
                (conversation, title) => RenameConversationAsync(project, conversation, title), ProjectTargets.Resolve(project.Project, saved.TargetId));
            project.Conversations.Add(item);
            project.IsExpanded = true;
            SelectConversation(project, item);
        });
    }

    public Task DuplicateConversationAsync(ConversationViewModel source, bool open) => ChangeSidebarAsync(async () =>
    {
        var project = Projects.FirstOrDefault(item => item.Conversations.Any(conversation => ReferenceEquals(conversation.Workspace, source)))
            ?? throw new KeyNotFoundException("The original conversation no longer exists.");
        var original = project.Conversations.Single(item => ReferenceEquals(item.Workspace, source));
        var suffix = open ? "fork" : "clone";
        var baseTitle = original.Title + " · " + suffix;
        var title = baseTitle;
        for (var number = 2; project.Conversations.Any(item => item.Title == title); number++) title = baseTitle + " " + number;
        var saved = new ConversationDraft { Id = Guid.NewGuid(), TargetId = original.Target?.Id ?? original.Conversation.TargetId ?? project.Project.Id, Title = title, CreatedAt = DateTimeOffset.UtcNow, IsTitleManual = true };
        await source.CopySessionAsync(sessionPaths.GetSessionFile(project.Project.Id, saved.Id), saved.Title);
        if (source.Artifacts is { } artifacts)
            await artifacts.Store.CopyToAsync(new Services.Conversations.ArtifactStore(Services.Conversations.ArtifactStore.ForSession(sessionPaths.GetSessionFile(project.Project.Id, saved.Id))));
        // Publish to the catalog only after Pi has produced an independent session file.
        // If registration fails, retain that file for recovery instead of risking deletion after an uncertain commit.
        await Task.Run(() => repository.AddConversationCopyAsync(project.Project.Id, original.Conversation.Id, saved));
        var item = new ConversationItemViewModel(saved, conversation => SelectConversation(project, conversation), workspaces?.GetOrCreate(project.Project, saved),
            (conversation, name) => RenameConversationAsync(project, conversation, name), ProjectTargets.Resolve(project.Project, saved.TargetId));
        project.Conversations.Add(item);
        project.IsExpanded = true;
        if (open && ReferenceEquals(Chat, source)) SelectConversation(project, item);
    });

    public Task RenameProjectAsync(ProjectItemViewModel project, string name) => ChangeSidebarAsync(async () =>
    {
        await Task.Run(() => repository.RenameProjectAsync(project.Project.Id, name));
        project.SetName(name.Trim());
        NotifySelection();
    });

    public Task RenameConversationAsync(ProjectItemViewModel project, ConversationItemViewModel conversation, string title) => ChangeSidebarAsync(async () =>
    {
        await titleGate.WaitAsync();
        try
        {
            await Task.Run(() => repository.RenameConversationAsync(project.Project.Id, conversation.Conversation.Id, title));
            conversation.SetTitle(title.Trim());
            if (ReferenceEquals(selectedConversation, conversation)) NotifyConversationTitle();
            if (conversation.Workspace is not null) await conversation.Workspace.SetSessionNameAsync(title.Trim());
        }
        finally { titleGate.Release(); }
    });

    private async void OnExplicitSessionNameChanged(Guid projectId, Guid conversationId, string title)
    {
        var project = Projects.FirstOrDefault(item => item.Project.Id == projectId);
        var conversation = project?.Conversations.FirstOrDefault(item => item.Conversation.Id == conversationId);
        if (project is null || conversation is null) return;
        try { await RenameConversationAsync(project, conversation, title); }
        catch (Exception exception) { ReportError(exception); }
    }

    private async void OnSessionNameChanged(Guid projectId, Guid conversationId, string title)
    {
        await titleGate.WaitAsync();
        try
        {
            var project = Projects.FirstOrDefault(item => item.Project.Id == projectId);
            var conversation = project?.Conversations.FirstOrDefault(item => item.Conversation.Id == conversationId);
            if (conversation is null || conversation.Conversation.IsTitleManual || conversation.Title == title) return;
            if (await Task.Run(() => repository.SetGeneratedTitleAsync(projectId, conversationId, title)))
            {
                conversation.SetTitle(title.Trim(), manual: false);
                if (ReferenceEquals(selectedConversation, conversation)) NotifyConversationTitle();
            }
        }
        catch (KeyNotFoundException) { /* The conversation was deleted while its naming request finished. */ }
        catch (Exception exception) { ReportError(exception); }
        finally { titleGate.Release(); }
    }

    public ProjectItemViewModel FindProject(ConversationItemViewModel conversation) =>
        Projects.FirstOrDefault(project => project.Conversations.Contains(conversation)) ?? throw new KeyNotFoundException("The conversation no longer exists.");

    public Task ToggleSettledAsync(ConversationItemViewModel conversation) => ChangeSidebarAsync(async () =>
    {
        var project = FindProject(conversation);
        var settled = !conversation.IsSettled;
        await Task.Run(() => repository.SetConversationSettledAsync(project.Project.Id, conversation.Conversation.Id, settled));
        conversation.SetSettled(settled);
        project.RefreshGroups();
        if (settled && ReferenceEquals(selectedConversation, conversation)) SetSelection(project, null);
    });

    public Task DeleteConversationAsync(ConversationItemViewModel conversation) => ChangeSidebarAsync(async () =>
    {
        var project = FindProject(conversation);
        if (dataCleanup is not null) await dataCleanup.ScheduleAsync(project.Project.Id, conversation.Conversation.Id,
            ProjectTargets.Resolve(project.Project, conversation.Conversation.TargetId ?? project.Project.Id));
        await Task.Run(() => repository.DeleteConversationAsync(project.Project.Id, conversation.Conversation.Id));
        project.Conversations.Remove(conversation);
        if (ReferenceEquals(selectedConversation, conversation)) SetSelection(project, null);
        if (workspaces is not null) await workspaces.RemoveAsync(project.Project.Id, conversation.Conversation.Id);
        await CleanupDeletedDataAsync();
    });

    /// <summary>Deletes catalog-owned history through the normal deferred cleanup boundary; never deletes project directories.</summary>
    public Task ClearCatalogAsync(bool removeProjects) => ChangeSidebarAsync(async () =>
    {
        if (HasActiveWork) throw new InvalidOperationException("Finish or cancel active conversations before deleting their data.");
        foreach (var project in Projects.ToArray())
        {
            foreach (var conversation in project.Conversations.ToArray())
            {
                if (HasActiveWork) throw new InvalidOperationException("A conversation became active. Deletion stopped; already deleted conversations remain deleted.");
                if (dataCleanup is not null) await dataCleanup.ScheduleAsync(project.Project.Id, conversation.Conversation.Id,
                    ProjectTargets.Resolve(project.Project, conversation.Conversation.TargetId ?? project.Project.Id));
                await Task.Run(() => repository.DeleteConversationAsync(project.Project.Id, conversation.Conversation.Id));
                project.Conversations.Remove(conversation);
                if (ReferenceEquals(selectedConversation, conversation)) { SetSelection(project, null); OpenSettings(); }
                if (workspaces is not null) await workspaces.RemoveAsync(project.Project.Id, conversation.Conversation.Id);
            }
            project.RefreshGroups();
            if (!removeProjects) continue;
            await Task.Run(() => repository.DeleteProjectAsync(project.Project.Id));
            Projects.Remove(project);
            if (ReferenceEquals(selectedProject, project)) { SetSelection(null, null); OpenSettings(); }
            if (workspaces is not null) await workspaces.RemoveAsync(project.Project.Id);
        }
        OnPropertyChanged(nameof(Projects)); OnPropertyChanged(nameof(ShowEmptyProjects));
        await CleanupDeletedDataAsync();
    });

    public Task DeleteProjectAsync(ProjectItemViewModel project) => ChangeSidebarAsync(async () =>
    {
        if (dataCleanup is not null)
            foreach (var conversation in project.Conversations)
                await dataCleanup.ScheduleAsync(project.Project.Id, conversation.Conversation.Id,
                    ProjectTargets.Resolve(project.Project, conversation.Conversation.TargetId ?? project.Project.Id));
        await Task.Run(() => repository.DeleteProjectAsync(project.Project.Id));
        Projects.Remove(project);
        if (ReferenceEquals(selectedProject, project)) SetSelection(Projects.FirstOrDefault(), null);
        OnPropertyChanged(nameof(ShowEmptyProjects));
        if (workspaces is not null) await workspaces.RemoveAsync(project.Project.Id);
        await CleanupDeletedDataAsync();
    });

    private Task? cleanupTask;
    public async Task<string> RetryCleanupAsync()
    {
        ErrorMessage = "";
        await CleanupDeletedDataAsync();
        return HasError ? ErrorMessage : "Pending conversation cleanup finished.";
    }
    private Task CleanupDeletedDataAsync() => cleanupTask is { IsCompleted: false } ? cleanupTask : cleanupTask = RunDataCleanupAsync();
    private async Task RunDataCleanupAsync()
    {
        if (dataCleanup is null) return;
        try { if (await dataCleanup.RunPendingAsync() is { } notice) ErrorMessage = notice; }
        catch (Exception error) { ErrorMessage = "Could not finish conversation data cleanup: " + error.Message; }
    }

    private async Task ChangeSidebarAsync(Func<Task> change)
    {
        if (!CanManageSidebar) throw new InvalidOperationException("Wait for the current sidebar change to finish.");
        isChanging = true;
        ErrorMessage = "";
        OnPropertyChanged(nameof(CanManageSidebar));
        try { await change(); }
        finally { isChanging = false; OnPropertyChanged(nameof(CanManageSidebar)); }
    }

    private void NotifyConversationTitle()
    {
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedTarget));
        OnPropertyChanged(nameof(TargetLabel));
        OnPropertyChanged(nameof(LocalWorkspacePath));
        OnPropertyChanged(nameof(SelectedProject));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(WorkspacePath));
        OnPropertyChanged(nameof(WelcomeTitle));
        OnPropertyChanged(nameof(RecentConversations));
        OnPropertyChanged(nameof(HasRecentConversations));
        OnPropertyChanged(nameof(WelcomeDescription));
        OnPropertyChanged(nameof(ShowCreateProject));
        OnPropertyChanged(nameof(ShowCreateConversation));
        OnPropertyChanged(nameof(HasConversation));
        OnPropertyChanged(nameof(Chat));
        OnPropertyChanged(nameof(ShowWelcome));
    }
}
