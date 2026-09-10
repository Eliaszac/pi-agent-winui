using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>Retains transcript, composer, and run state independently of the selected conversation.</summary>
public sealed class ConversationViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IConversationSession session;
    private readonly IUiDispatcher dispatcher;
    private readonly ConcurrentQueue<ConversationUpdate> updates = new();
    private readonly Dictionary<string, ChatEntryViewModel> entries = [];
    private readonly TranscriptPresentation presentation = new();
    public ObservableCollection<ChatEntryViewModel> DisplayEntries { get; } = [];
    private int drainScheduled;
    private bool disposed;
    private bool initialized;
    private bool busy;
    private bool operationInFlight;
    private bool stopping;
    private bool preparing;
    private bool running;
    private bool connected;
    private bool isViewed;
    private bool unseenCompletion;
    private readonly HashSet<string> runEntryIds = [];
    private bool trackingRun;
    public RunChangesViewModel? RunChanges { get; private set; }
    public bool HasRunChanges => RunChanges is { Files.Count: > 0 } && !running;
    public bool ShowBlockedIndicator => !isViewed && Prompts.Count > 0;
    public bool ShowRunningIndicator => !isViewed && !ShowBlockedIndicator && running;
    public bool ShowDoneIndicator => !isViewed && !ShowBlockedIndicator && !running && unseenCompletion && !HasError;
    public string SidebarStatus => ShowBlockedIndicator ? "Approval needed" : ShowRunningIndicator ? "Running" : ShowDoneIndicator ? "Done" : "";

    public void SetViewed(bool viewed)
    {
        isViewed = viewed;
        if (viewed) unseenCompletion = false;
        NotifyIndicators();
    }

    private void NotifyIndicators()
    {
        OnPropertyChanged(nameof(ShowBlockedIndicator));
        OnPropertyChanged(nameof(ShowRunningIndicator));
        OnPropertyChanged(nameof(ShowDoneIndicator));
        OnPropertyChanged(nameof(SidebarStatus));
    }
    private string draft = "";
    private string status = "Loading conversation…";
    private PiModel? model;
    private string? thinkingLevel;
    public IReadOnlyList<string> ThinkingLevels { get; private set; } = [];
    public string? SelectedThinkingLevel => ThinkingLevels.Count == 1 && ThinkingLevels[0] == "off"
        ? "off" : ThinkingLevels.Contains(thinkingLevel ?? "") ? thinkingLevel : null;
    public bool CanChangeThinkingLevel => IsReady && !busy && !running && !disposed && ThinkingLevels.Count > 1;
    public AsyncRelayCommand SelectThinkingLevelCommand { get; }
    private bool approvalAvailable;
    private string? approvalMode;
    private string approvalStatus = "Install approval extension";
    public IReadOnlyList<string> ApprovalModes => PermissionModesSupport.Modes;
    public string? SelectedApprovalMode => approvalMode;
    public bool HasApprovalModes => approvalAvailable;
    public bool ShowApprovalSetup => !approvalAvailable;
    public string ApprovalStatus => approvalStatus;
    public bool CanChangeApprovalMode => IsReady && !busy && !running && !disposed && approvalAvailable;
    private string error = "";
    private string warning = "";
    public string Warning => warning;
    public bool HasInlineWarning => IsReady && warning.Length > 0;
    public ObservableCollection<ChatEntryViewModel> Entries { get; } = [];
    public ObservableCollection<ExtensionPromptViewModel> Prompts { get; } = [];
    public string Draft
    {
        get => draft;
        set
        {
            if (!SetProperty(ref draft, value)) return;
            OnPropertyChanged(nameof(CanSend));
            OnPropertyChanged(nameof(CanUseComposerAction));
        }
    }
    public string Status => status;
    public PiModel? SelectedModel => AvailableModels.FirstOrDefault(item => item.Provider == model?.Provider && item.Id == model?.Id);
    public int SelectedModelIndex => SelectedModel is { } selected ? AvailableModels.IndexOf(selected) : -1;
    public IReadOnlyList<string> ModelOptions { get; private set; } = [];
    public ObservableCollection<PiModel> AvailableModels { get; } = [];
    public bool CanChangeModel => IsReady && !busy && !running && !disposed && AvailableModels.Count > 0;
    public string Error => error;
    public bool HasError => error.Length > 0;
    public bool NeedsAttention => HasError || Prompts.Count > 0;
    public bool IsRunning => running;
    public bool IsConnected => connected;
    public bool IsReady => connected && !preparing;
    public bool IsLoading => preparing || (!connected && !HasError);
    public bool ShowRecovery => !IsLoading && !connected;
    public bool HasInlineError => IsReady && HasError;
    public bool CanSend => IsReady && !busy && !running && !disposed && !string.IsNullOrWhiteSpace(Draft);
    public bool CanStop => connected && running && (!busy || operationInFlight) && !stopping;
    public AsyncRelayCommand ComposerActionCommand => running ? StopCommand : SendCommand;
    public bool CanUseComposerAction => running ? CanStop : CanSend;
    public string ComposerActionLabel => running ? "Stop" : "Send";
    public string ComposerActionGlyph => running ? "\uE71A" : "\uE72A";
    public bool CanRetry => ShowRecovery && !busy && !disposed;
    public bool IsEmpty => Entries.Count == 0 && !previewCompacting && !compacting;
    private readonly bool previewCompacting;
    private bool compacting;
    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand SendCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand SelectModelCommand { get; }
    public AsyncRelayCommand SelectApprovalModeCommand { get; }
    public event Action? TranscriptChanged;
    public Func<bool, Task>? DuplicateConversation { get; set; }
    public AsyncRelayCommand ForkCommand { get; }
    public AsyncRelayCommand CloneCommand { get; }
    private ChatEntryViewModel? responseActionsEntry;
    public bool CanDuplicateConversation => CanUseCommands && Prompts.Count == 0 &&
        Entries.LastOrDefault(entry => entry.IsAssistant || entry.IsUser) is { CanCopyResponse: true, Status.Length: 0 };

    public async Task CopySessionAsync(string destination, string title)
    {
        if (!CanDuplicateConversation) throw new InvalidOperationException("Wait for a completed response before copying this conversation.");
        await ExecuteAsync(() => session.CopySessionAsync(destination, title));
    }
    public event Action<string>? SessionNameChanged;
    public event Action<string>? ExplicitSessionNameChanged;
    public Func<string, Task<bool>>? HandleComposerCommand { get; set; }
    public Task SetSessionNameAsync(string name) => session.SetSessionNameAsync(name);
    public bool CanUseCommands => IsReady && !busy && !running && !disposed;
    private bool modelsNeedRefresh;
    public void InvalidateProviderModels()
    {
        modelsNeedRefresh = true;
        TryRefreshProviderModels();
    }
    private void TryRefreshProviderModels()
    {
        if (!modelsNeedRefresh || !CanUseCommands) return;
        modelsNeedRefresh = false;
        _ = RefreshProviderModelsAsync();
    }
    private async Task RefreshProviderModelsAsync()
    {
        try { await RunOperationAsync(ConversationOperation.RefreshModels); }
        catch (Exception exception) { ReportError(exception); }
    }
    public async Task<System.Text.Json.JsonElement> RunOperationAsync(ConversationOperation operation, string? argument = null)
    {
        if (!CanUseCommands) throw new InvalidOperationException("Wait for this conversation to finish before using commands.");
        System.Text.Json.JsonElement result = default;
        operationInFlight = true;
        try { await ExecuteAsync(async () => result = await session.RunOperationAsync(operation, argument).ConfigureAwait(false)); }
        finally { operationInFlight = false; NotifyState(); }
        return result;
    }

    public async Task SendExtensionCommandAsync(string command)
    {
        if (!CanUseCommands) throw new InvalidOperationException("Wait for this conversation to finish before using commands.");
        string? generatedTitle = null;
        await ExecuteAsync(async () =>
        {
            var oldTitle = command == "/auto-name" ? PiJson.Text(await session.RunOperationAsync(ConversationOperation.State), "sessionName") : null;
            await session.SendAsync(command).ConfigureAwait(false);
            if (command != "/auto-name") return;
            var state = await session.RunOperationAsync(ConversationOperation.State).ConfigureAwait(false);
            var title = PiJson.Text(state, "sessionName");
            if (!string.IsNullOrWhiteSpace(title) && title != oldTitle) generatedTitle = title;
        });
        if (generatedTitle is not null) ExplicitSessionNameChanged?.Invoke(generatedTitle);
    }

    public ConversationViewModel(IConversationSession session, IUiDispatcher dispatcher, bool previewCompacting = false)
    {
        this.previewCompacting = previewCompacting;
        this.session = session;
        this.dispatcher = dispatcher;
        session.Updated += Enqueue;
        ForkCommand = new AsyncRelayCommand(_ => RequestDuplicateAsync(true), ReportError);
        CloneCommand = new AsyncRelayCommand(_ => RequestDuplicateAsync(false), ReportError);
        RetryCommand = new AsyncRelayCommand(_ => PrepareAsync(), ReportError);
        SendCommand = new AsyncRelayCommand(async _ =>
        {
            if (!CanSend) return;
            var submitted = Draft;
            if (HandleComposerCommand is { } handler && await handler(submitted)) return;
            await ExecuteAsync(async () =>
            {
                await session.SendAsync(submitted).ConfigureAwait(false);
                dispatcher.Post(() => { if (Draft == submitted) Draft = ""; });
            });
        }, ReportError);
        StopCommand = new AsyncRelayCommand(async _ =>
        {
            if (!CanStop) return;
            stopping = true;
            NotifyState();
            try { await Task.Run(() => session.StopAsync()); }
            finally { stopping = false; NotifyState(); }
        }, ReportError);
        SelectThinkingLevelCommand = new AsyncRelayCommand(async value =>
        {
            try
            {
                if (value is string next && CanChangeThinkingLevel && ThinkingLevels.Contains(next) && next != thinkingLevel)
                    await ExecuteAsync(() => session.SetThinkingLevelAsync(next));
            }
            finally { OnPropertyChanged(nameof(SelectedThinkingLevel)); }
        }, ReportError);
        SelectModelCommand = new AsyncRelayCommand(async value =>
        {
            if (value is PiModel next && CanChangeModel && AvailableModels.Contains(next) && next != SelectedModel)
                await ExecuteAsync(() => session.SetModelAsync(next.Provider, next.Id));
            OnPropertyChanged(nameof(SelectedModel));
        }, ReportError);
        SelectApprovalModeCommand = new AsyncRelayCommand(async value =>
        {
            if (value is string next && CanChangeApprovalMode && ApprovalModes.Contains(next) && next != approvalMode)
                await ExecuteAsync(() => session.SetApprovalModeAsync(next));
            OnPropertyChanged(nameof(SelectedApprovalMode));
        }, ReportError);
    }

    public Task InitializeAsync()
    {
        if (initialized || disposed) return Task.CompletedTask;
        initialized = true;
        return RetryCommand.ExecuteAsync();
    }

    public Task RequestDuplicateAsync(bool open)
    {
        if (!CanDuplicateConversation) throw new InvalidOperationException("Wait for a completed response before copying this conversation.");
        return DuplicateConversation?.Invoke(open) ?? throw new InvalidOperationException("Conversation copying is unavailable.");
    }

    private async Task PrepareAsync()
    {
        if (busy || disposed || connected) return;
        preparing = true;
        busy = true;
        error = "";
        NotifyState();
        try { await Task.Run(() => session.ConnectAsync()); }
        catch (Exception exception)
        {
            // Keep failures behind preceding startup events in the same ordered presentation queue.
            Enqueue(new() { IsConnected = false, Error = exception.Message });
        }
        finally
        {
            preparing = false;
            busy = false;
            NotifyState();
        }
    }

    private async Task ExecuteAsync(Func<Task> operation)
    {
        if (busy || disposed) return;
        busy = true;
        error = "";
        NotifyState();
        try { await Task.Run(operation); }
        finally { busy = false; NotifyState(); }
    }

    private void ReportError(Exception exception)
    {
        error = exception.Message;
        if (!connected) status = "Disconnected";
        NotifyState();
    }

    private void Enqueue(ConversationUpdate update)
    {
        if (disposed) return;
        updates.Enqueue(update);
        if (Interlocked.Exchange(ref drainScheduled, 1) == 0 && !dispatcher.Post(Drain))
        {
            updates.Clear();
            Interlocked.Exchange(ref drainScheduled, 0);
        }
    }

    private void Drain()
    {
        // A bounded batch keeps streaming background conversations from monopolizing the UI thread.
        var changed = false;
        for (var count = 0; count < 128 && updates.TryDequeue(out var update); count++)
        {
            if (disposed) continue;
            if (!string.IsNullOrWhiteSpace(update.SessionName)) SessionNameChanged?.Invoke(update.SessionName);
            if (update.History is not null)
            {
                RunChanges = null;
                runEntryIds.Clear();
                trackingRun = false;
                entries.Clear();
                Entries.Clear();
                foreach (var entry in update.History) Upsert(entry);
                changed = true;
            }
            if (update.Entry is not null)
            {
                Upsert(update.Entry);
                if (trackingRun) runEntryIds.Add(update.Entry.Id);
                changed = true;
            }
            if (update.Status is not null) status = update.Status;
            if (update.IsCompacting is bool isCompacting && compacting != isCompacting)
            {
                compacting = isCompacting;
                changed = true;
            }
            if (update.IsRunning is bool isRunning)
            {
                if (!isRunning && compacting) { compacting = false; changed = true; }
                if (running != isRunning) changed = true;
                if (isRunning && !running)
                {
                    runEntryIds.Clear();
                    trackingRun = true;
                    warning = "";
                    if (update.Entry is not null) runEntryIds.Add(update.Entry.Id);
                    RunChanges = null;
                    changed = true;
                }
                running = isRunning;
                if (isRunning) unseenCompletion = false;
            }
            if (update.TurnCompleted)
            {
                unseenCompletion = !isViewed;
                if (trackingRun)
                {
                    if (update.RunUsage is { } usage)
                        Entries.LastOrDefault(entry => runEntryIds.Contains(entry.Id) && entry.CanCopyResponse)?.SetUsage(usage);
                    RunChanges = new(Entries.Where(entry => runEntryIds.Contains(entry.Id))
                        .Select(entry => entry.FileChange).OfType<FileChange>());
                    trackingRun = false;
                    changed = true;
                }
            }
            if (update.IsConnected is bool isConnected)
            {
                connected = isConnected;
                if (!connected) { ClearPrompts(); compacting = false; changed = true; }
            }
            if (update.Error is not null) error = update.Error;
            if (update.Warning is not null) warning = update.Warning;
            if (update.HasThinkingLevelUpdate) thinkingLevel = update.ThinkingLevel;
            if (update.ThinkingLevels is not null && !ThinkingLevels.SequenceEqual(update.ThinkingLevels))
            {
                ThinkingLevels = update.ThinkingLevels;
                OnPropertyChanged(nameof(ThinkingLevels));
            }
            if (update.ApprovalAvailable is bool availableApproval) approvalAvailable = availableApproval;
            if (update.HasApprovalModeUpdate) approvalMode = update.ApprovalMode;
            if (update.ApprovalStatus is not null) approvalStatus = update.ApprovalStatus;
            if (update.AvailableModels is not null)
            {
                AvailableModels.Clear();
                foreach (var available in update.AvailableModels) AvailableModels.Add(available);
            }
            if (update.HasModelUpdate) model = update.Model;
            if (model is not null && !AvailableModels.Any(item => item.Provider == model.Provider && item.Id == model.Id)) AvailableModels.Add(model);
            if (update.DismissPrompts) ClearPrompts();
            if (update.Prompt is not null && !Prompts.Any(item => item.Id == update.Prompt.Id))
            {
                var prompt = new ExtensionPromptViewModel(update.Prompt,
                    (id, response) => Task.Run(() => session.ReplyAsync(id, response)), RemovePrompt, ReportError);
                Prompts.Add(prompt);
                _ = prompt.ExpireAsync(action => dispatcher.Post(action), RemovePrompt);
            }
        }
        var modelOptions = AvailableModels.Select(item => $"Model: {item.DisplayName}").ToArray();
        if (!ModelOptions.SequenceEqual(modelOptions))
        {
            ModelOptions = modelOptions;
            OnPropertyChanged(nameof(ModelOptions));
        }
        NotifyState();
        OnPropertyChanged(nameof(RunChanges));
        OnPropertyChanged(nameof(HasRunChanges));
        if (changed)
        {
            var rows = presentation.Project(Entries, running || compacting || previewCompacting,
                compacting || previewCompacting ? "Compacting context…" : "Processing…");
            for (var index = 0; index < rows.Count; index++)
            {
                if (index < DisplayEntries.Count && ReferenceEquals(DisplayEntries[index], rows[index])) continue;
                var existingIndex = DisplayEntries.IndexOf(rows[index]);
                if (existingIndex >= 0) DisplayEntries.Move(existingIndex, index);
                else DisplayEntries.Insert(index, rows[index]);
            }
            while (DisplayEntries.Count > rows.Count) DisplayEntries.RemoveAt(DisplayEntries.Count - 1);
            OnPropertyChanged(nameof(IsEmpty));
            TranscriptChanged?.Invoke();
        }
        Interlocked.Exchange(ref drainScheduled, 0);
        if (!updates.IsEmpty && Interlocked.Exchange(ref drainScheduled, 1) == 0) dispatcher.Post(Drain);
    }

    private void Upsert(ChatEntry entry)
    {
        if (entries.TryGetValue(entry.Id, out var existing)) existing.Update(entry);
        else { var item = new ChatEntryViewModel(entry) { ForkCommand = ForkCommand, CloneCommand = CloneCommand }; entries.Add(entry.Id, item); Entries.Add(item); }
    }

    private void RemovePrompt(ExtensionPromptViewModel prompt)
    {
        if (Prompts.Remove(prompt)) prompt.Dispose();
        OnPropertyChanged(nameof(NeedsAttention));
        NotifyIndicators();
        TryRefreshProviderModels();
    }

    private void ClearPrompts() { foreach (var prompt in Prompts) prompt.Dispose(); Prompts.Clear(); }

    private void NotifyState()
    {
        var latest = IsReady && !running && Prompts.Count == 0
            ? Entries.LastOrDefault(entry => entry.IsAssistant || entry.IsUser) : null;
        if (latest is not { CanCopyResponse: true, Status.Length: 0 }) latest = null;
        if (!ReferenceEquals(responseActionsEntry, latest)) responseActionsEntry?.SetConversationActions(false, false);
        responseActionsEntry = latest;
        latest?.SetConversationActions(true, CanDuplicateConversation);
        OnPropertyChanged(nameof(CanDuplicateConversation));
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasInlineWarning));
        foreach (var property in new[] { nameof(Status), nameof(SelectedModel), nameof(SelectedModelIndex), nameof(CanChangeModel), nameof(Error), nameof(HasError), nameof(IsRunning),
            nameof(IsConnected), nameof(CanSend), nameof(CanStop), nameof(CanRetry), nameof(NeedsAttention), nameof(CanUseCommands),
            nameof(IsReady), nameof(IsLoading), nameof(ShowRecovery), nameof(HasInlineError), nameof(HasApprovalModes),
            nameof(ShowApprovalSetup), nameof(SelectedApprovalMode), nameof(CanChangeApprovalMode), nameof(ApprovalStatus),
            nameof(SelectedThinkingLevel), nameof(CanChangeThinkingLevel) }) OnPropertyChanged(property);
        OnPropertyChanged(nameof(ComposerActionCommand));
        OnPropertyChanged(nameof(CanUseComposerAction));
        OnPropertyChanged(nameof(ComposerActionLabel));
        OnPropertyChanged(nameof(ComposerActionGlyph));
        NotifyIndicators();
        TryRefreshProviderModels();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        session.Updated -= Enqueue;
        ClearPrompts();
        await session.DisposeAsync();
    }
}
