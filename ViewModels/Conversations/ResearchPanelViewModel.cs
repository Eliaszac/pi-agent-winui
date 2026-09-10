using PiAgentGui.Models.Conversations;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ResearchPanelViewModel : ObservableObject
{
    private readonly ResearchCoordinator coordinator;
    private IReadOnlyList<ResearchTask> all = [];
    private Guid? conversationId;
    private ResearchTask? selected;
    private bool open;
    private string error = "";
    public string Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsOpen { get => open; set => SetProperty(ref open, value); }
    public bool Enabled => coordinator.Enabled;
    public bool IsDisabled => !Enabled;
    public string EnabledLabel => Enabled ? "On" : "Off";
    public string EnableActionLabel => Enabled ? "Turn off background research" : "Turn on background research";
    public bool HasTasks => Tasks.Count > 0;
    public bool IsEmpty => !HasTasks;
    public bool HasSelection => Selected is not null;
    public string EmptyHint => Enabled ? "Ask your agent to research something in the background." : "Turn research on to allow independent background tasks.";
    public IReadOnlyList<ResearchTask> Tasks => all.Where(task => task.ConversationId == conversationId).OrderByDescending(task => task.CreatedAt).ToArray();
    public ResearchTask? Selected { get => selected; set { SetProperty(ref selected, value); OnPropertyChanged(nameof(CanShare)); OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(HasSelection)); } }
    public bool CanShare => Selected is { Status: "Completed" };
    public bool CanCancel => Selected?.Status is "Running" or "Queued";
    public AsyncRelayCommand ToggleEnabledCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public ResearchPanelViewModel(ResearchCoordinator coordinator, IUiDispatcher dispatcher)
    {
        this.coordinator = coordinator;
        coordinator.Failed += message => dispatcher.Post(() => Error = message);
        coordinator.Changed += snapshot => dispatcher.Post(() =>
        {
            all = snapshot;
            OnPropertyChanged(nameof(Tasks)); OnPropertyChanged(nameof(Enabled)); OnPropertyChanged(nameof(IsDisabled));
            OnPropertyChanged(nameof(HasTasks)); OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EnabledLabel)); OnPropertyChanged(nameof(EnableActionLabel)); OnPropertyChanged(nameof(EmptyHint));
            Selected = Tasks.FirstOrDefault(task => task.Id == Selected?.Id) ?? Tasks.FirstOrDefault();
        });
        ToggleEnabledCommand = new(async _ => { await coordinator.SetEnabledAsync(!Enabled); Error = ""; }, exception => Error = exception.Message);
        CancelCommand = new(async _ => { if (Selected is { } task) await coordinator.CancelAsync(task.Id); }, exception => Error = exception.Message);
    }
    public void SelectConversation(Guid? id)
    {
        conversationId = id; OnPropertyChanged(nameof(Tasks)); Selected = Tasks.FirstOrDefault();
        OnPropertyChanged(nameof(HasTasks)); OnPropertyChanged(nameof(IsEmpty));
    }
    public void ReportError(string message) => Error = message;
}
