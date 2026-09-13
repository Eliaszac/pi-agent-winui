using PiAgentGui.Models.Applications;
using PiAgentGui.Services.Applications;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Applications;

public sealed class OpenInViewModel(IApplicationLocator locator, OpenInPreferenceStore preferences, ProjectApplicationLauncher launcher) : ObservableObject
{
    private string? directory;
    private string? preferred;
    private InstalledApplication? selected;
    private int revision;
    private bool opening;
    internal IReadOnlyList<InstalledApplication> Applications { get; private set; } = [];
    public string Label => selected is null ? "Open in" : $"Open in {selected.Name}";
    public string Logo => selected?.Logo ?? "explorer.svg";
    public bool CanOpen => selected is not null && directory is not null && !opening;
    public event Action<string>? Failed;
    public string? PreferredEditor => preferred;
    public IReadOnlyList<EditorPreferenceOption> EditorOptions
    {
        get
        {
            var result = new List<EditorPreferenceOption> { new(null, "System default") };
            result.AddRange(Applications.Where(app => app.Kind is ApplicationKind.Editor or ApplicationKind.SolutionEditor)
                .Select(app => new EditorPreferenceOption(app.Id, app.Name)));
            if (preferred is not null && result.All(option => option.Id != preferred)) result.Add(new(preferred, preferred + " (unavailable)", false));
            return result;
        }
    }
    public async Task SetPreferredEditorAsync(string? id)
    {
        if (id is not null && !EditorOptions.Any(option => option.Id == id && option.Available))
            throw new InvalidOperationException("Choose an available editor or System default.");
        await Task.Run(() => preferences.Save(id));
        preferred = id;
        await RefreshAsync(directory);
    }
    public Task RefreshEditorsAsync() => RefreshAsync(directory, discover: true);

    public async Task RefreshAsync(string? path, bool discover = false)
    {
        var current = ++revision;
        directory = path;
        Notify();
        try
        {
            var result = await Task.Run(() =>
            {
                var apps = discover || Applications.Count == 0 ? locator.Discover() : Applications;
                string? saved;
                try { saved = preferences.Read(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { saved = preferred; }
                return (apps, saved, associated: path is null ? null : locator.GetAssociatedExecutable(path));
            });
            if (current != revision) return;
            Applications = result.apps;
            preferred = result.saved;
            selected = OpenInSelection.Resolve(Applications, preferred, result.associated);
            Notify();
        }
        catch (Exception) { if (current == revision) Failed?.Invoke("Couldn't discover applications. Try refreshing the application list."); }
    }

    public async Task OpenAsync(string? id = null)
    {
        var application = id is null ? selected : Applications.FirstOrDefault(app => app.Id == id);
        var path = directory;
        if (application is null || path is null || opening) return;
        opening = true;
        Notify();
        try
        {
            await Task.Run(() => launcher.Open(application, path));
            if (application.Kind is ApplicationKind.Editor or ApplicationKind.SolutionEditor)
            {
                ++revision;
                selected = application;
                preferred = application.Id;
                try { await Task.Run(() => preferences.Save(application.Id)); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { Failed?.Invoke("The editor opened, but its preference couldn't be saved."); }
            }
        }
        catch (Exception) { Failed?.Invoke($"Couldn't open {application.Name}. Check that it and the project folder still exist, then refresh the application list."); }
        finally { opening = false; Notify(); }
    }

    public Task OpenFileAsync(string path)
    {
        var editor = selected?.Kind is ApplicationKind.Editor or ApplicationKind.SolutionEditor ? selected
            : Applications.FirstOrDefault(app => app.Kind is ApplicationKind.Editor or ApplicationKind.SolutionEditor);
        if (editor is null) throw new IOException("No editor was detected. Choose an installed editor from Open in.");
        return Task.Run(() => launcher.OpenFile(editor, path));
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Logo));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(EditorOptions));
    }
}
