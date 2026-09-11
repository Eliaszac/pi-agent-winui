using System.Collections.ObjectModel;
using PiAgentGui.Models.Pi;
using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Processes;

public sealed class ProcessesPanelViewModel(AgentProcessReader reader, AgentProcessStopper stopper) : ObservableObject
{
    private bool open;
    private bool refreshing;
    private bool stopping;
    private string error = "";
    public bool CanStop => !stopping;
    public string Error { get => error; private set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public void ReportError(string text) => Error = text;
    private int revision;
    private ProcessIdentity? root;
    private readonly HashSet<ProcessIdentity> known = [];
    private string message = "No running subprocesses.";
    public ObservableCollection<AgentProcess> Items { get; } = [];
    public bool IsOpen { get => open; set { if (SetProperty(ref open, value)) revision++; } }
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public bool ShowMessage => Items.Count == 0;

    public void Select(ProcessIdentity? identity)
    {
        if (root == identity) return;
        root = identity; revision++; known.Clear(); Items.Clear(); Error = "";
        Message = identity is null ? "Open a connected conversation to see its processes." : "Checking processes…";
        OnPropertyChanged(nameof(ShowMessage));
    }

    public async Task RefreshAsync()
    {
        if (!IsOpen || refreshing || root is not { } identity) return;
        refreshing = true;
        var version = revision;
        var previous = known.ToHashSet();
        try
        {
            var rows = await Task.Run(() => reader.Read(identity, previous));
            if (version != revision) return;
            known.Clear(); known.UnionWith(rows.Select(row => row.Identity));
            var existing = Items.ToDictionary(item => item.Identity);
            var stable = rows.Select(row => existing.TryGetValue(row.Identity, out var old) ? old : row).ToArray();
            ObservableCollectionSynchronizer.Synchronize(Items, stable);
            foreach (var row in Items) row.RefreshElapsed();
            Message = "No running subprocesses.";
            OnPropertyChanged(nameof(ShowMessage));
        }
        catch (Exception)
        {
            if (version != revision) return;
            Items.Clear(); Message = "Couldn't read running processes. Retrying…";
            OnPropertyChanged(nameof(ShowMessage));
        }
        finally { refreshing = false; }
    }

    public async Task StopAsync(AgentProcess process)
    {
        if (stopping || root is not { } owner || !known.Contains(process.Identity)) return;
        stopping = true; OnPropertyChanged(nameof(CanStop)); Error = "";
        var owned = known.ToHashSet();
        try { await Task.Run(() => stopper.Stop(owner, process.Identity, owned)); }
        catch (Exception exception) { if (root == owner) Error = exception.Message; }
        finally { stopping = false; OnPropertyChanged(nameof(CanStop)); }
        await RefreshAsync();
    }
}
