using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Projects;

/// <summary>Shared inline naming interaction with explicit commit, cancel, and recoverable errors.</summary>
public sealed class InlineRenameViewModel : ObservableObject
{
    private readonly Func<string> currentName;
    private readonly Func<string, Task> save;
    private string draft = "";
    private string error = "";
    private bool editing;
    private bool saving;
    public string Draft { get => draft; set => SetProperty(ref draft, value); }
    public string Error => error;
    public bool HasError => error.Length > 0;
    public bool IsEditing => editing;
    public bool IsNotEditing => !editing;
    public bool CanEdit => !saving;
    public RelayCommand BeginCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public InlineRenameViewModel(Func<string> currentName, Func<string, Task> save)
    {
        this.currentName = currentName;
        this.save = save;
        BeginCommand = new RelayCommand(_ => { if (saving) return; Draft = this.currentName(); error = ""; editing = true; Notify(); });
        CancelCommand = new RelayCommand(_ => { if (saving) return; editing = false; error = ""; Notify(); });
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), exception => { error = exception.Message; Notify(); });
    }

    private async Task SaveAsync()
    {
        if (!editing || saving) return;
        var name = Draft.Trim();
        if (string.IsNullOrWhiteSpace(name)) { error = "Enter a name."; Notify(); return; }
        saving = true;
        error = "";
        Notify();
        try
        {
            if (name != currentName()) await save(name);
            editing = false;
        }
        finally { saving = false; Notify(); }
    }

    private void Notify()
    {
        foreach (var property in new[] { nameof(IsEditing), nameof(IsNotEditing), nameof(CanEdit), nameof(Error), nameof(HasError) }) OnPropertyChanged(property);
    }
}
