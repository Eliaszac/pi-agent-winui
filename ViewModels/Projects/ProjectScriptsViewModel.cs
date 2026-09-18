using System.Collections.ObjectModel;
using PiAgentGui.Models.Projects;
using PiAgentGui.Repositories.Projects;
using PiAgentGui.Utilities;
using PiAgentGui.ViewModels.Terminal;

namespace PiAgentGui.ViewModels.Projects;

public sealed class ProjectScriptsViewModel(IProjectRepository repository, TerminalPanelViewModel terminals) : ObservableObject
{
    private Guid? projectId;
    private ExecutionTarget? target;
    private Guid? scriptTargetId;
    public bool IsRemoteTarget => target is { IsLocal: false };
    public string CommandHeader => IsRemoteTarget ? "Bash command" : "PowerShell command";
    private string projectDirectory = "";
    private Guid? selectedId;
    private int revision;
    private bool loading;
    private bool saving;
    private bool loaded;
    private string error = "";
    public ObservableCollection<ProjectScript> Scripts { get; } = [];
    public string Label => Scripts.FirstOrDefault(item => item.Id == selectedId)?.Name ?? Scripts.FirstOrDefault()?.Name ?? "Add scripts";
    public bool CanUse => projectId is not null && loaded && !loading && !saving;
    public string ActionHint => Scripts.Count == 0 ? "Add project scripts" : "Run the selected project script";
    public string ActionGlyph => Scripts.Count == 0 ? "\uE710" : "\uE768";
    public string Error => error;
    public bool HasError => error.Length > 0;

    public Task<IReadOnlyList<PackageScriptCandidate>> PreviewPackageAsync(string file)
    {
        if (IsRemoteTarget) throw new InvalidOperationException("Package import uses local files. Add a Bash command manually for this target.");
        var directory = projectDirectory;
        return Task.Run(() => PackageScriptImporter.ReadAsync(file, directory));
    }

    public async Task<int> ImportAsync(IReadOnlyList<ProjectScript> scripts)
    {
        if (!CanUse || projectId is not { } owner) throw new InvalidOperationException("Wait for the project's scripts to load.");
        var next = PackageScriptImporter.Merge(projectDirectory, Scripts.ToArray(), scripts);
        var added = next.Count - Scripts.Count;
        if (added == 0) return 0;
        await PersistAsync(owner, new() { Scripts = next, SelectedId = selectedId ?? next.FirstOrDefault()?.Id });
        return added;
    }

    public async Task SelectAsync(Guid? id, string? directory, ExecutionTarget? executionTarget = null)
    {
        var request = ++revision;
        target = executionTarget;
        scriptTargetId = executionTarget?.Id == id ? null : executionTarget?.Id;
        projectId = id; projectDirectory = directory ?? "";
        Scripts.Clear(); selectedId = null; loaded = false; loading = id is not null; error = ""; Notify();
        if (id is null) return;
        try
        {
            var projects = await repository.GetAllAsync();
            var project = projects.FirstOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("The project no longer exists.");
            var settings = ProjectScripts.Read(project.Metadata, scriptTargetId);
            if (request != revision) return;
            Apply(settings); loaded = true;
        }
        catch (Exception exception) { if (request == revision) error = exception.Message; }
        finally { if (request == revision) { loading = false; Notify(); } }
    }

    public async Task SaveAsync(Guid? scriptId, string name, string command, string directory)
    {
        if (!CanUse || projectId is not { } owner) throw new InvalidOperationException("Wait for the project's scripts to load.");
        var script = new ProjectScript(scriptId ?? Guid.NewGuid(), name.Trim(), command, directory.Trim());
        var next = Scripts.ToList();
        var index = next.FindIndex(item => item.Id == script.Id);
        if (index >= 0) next[index] = script; else next.Add(script);
        var settings = new ProjectScriptSettings { Scripts = next, SelectedId = selectedId ?? script.Id };
        ProjectScripts.Validate(settings);
        _ = ProjectScripts.ResolveTargetDirectory(target, projectDirectory, directory);
        await PersistAsync(owner, settings);
    }

    public async Task DeleteAsync(Guid id)
    {
        if (!CanUse || projectId is not { } owner) throw new InvalidOperationException("Wait for the project's scripts to load.");
        var next = Scripts.Where(item => item.Id != id).ToArray();
        await PersistAsync(owner, new() { Scripts = next, SelectedId = selectedId == id ? next.FirstOrDefault()?.Id : selectedId });
    }

    public IReadOnlyList<ProjectScript> Search(string query)
    {
        if (!CanUse) throw new InvalidOperationException(HasError ? Error : "Wait for the project's scripts to load.");
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Scripts.Where(script => terms.All(term => script.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Take(20).ToArray();
    }

    public ProjectScriptRunRequest PrepareRun(ProjectScript script)
    {
        if (!CanUse || !Scripts.Contains(script)) throw new InvalidOperationException("The script changed or is unavailable. Search again before running it.");
        return new(script, ProjectScripts.ResolveTargetDirectory(target, projectDirectory, script.WorkingDirectory),
            target?.Label ?? "Local Windows", revision, terminals.ConversationId);
    }

    public async Task RunReviewedAsync(ProjectScriptRunRequest request)
    {
        ValidateReview(request);
        await RunAsync(request.Script.Id);
    }

    private void ValidateReview(ProjectScriptRunRequest request)
    {
        if (!CanUse || request.Revision != revision || request.ConversationId != terminals.ConversationId
            || !Scripts.Contains(request.Script)
            || request.Directory != ProjectScripts.ResolveTargetDirectory(target, projectDirectory, request.Script.WorkingDirectory))
            throw new InvalidOperationException("The script, target, or conversation changed. Review the script again before running it.");
    }

    public async Task RunAsync(Guid? id = null)
    {
        if (!CanUse || projectId is not { } owner) return;
        var script = Scripts.FirstOrDefault(item => item.Id == (id ?? selectedId)) ?? (id is null ? Scripts.FirstOrDefault() : null);
        if (script is null) throw new InvalidOperationException("Choose a saved script first.");
        var savedTarget = target;
        var version = revision;
        var conversation = terminals.ConversationId;
        var directory = ProjectScripts.ResolveTargetDirectory(savedTarget, projectDirectory, script.WorkingDirectory);
        await PersistAsync(owner, new() { Scripts = Scripts.ToArray(), SelectedId = script.Id });
        if (version != revision || conversation != terminals.ConversationId)
            throw new InvalidOperationException("The target or conversation changed. Run the script again from its original conversation.");
        terminals.RunScript(owner, script.Id, script.Name, directory, script.Command, savedTarget);
    }

    private async Task PersistAsync(Guid owner, ProjectScriptSettings settings)
    {
        var version = revision;
        var targetId = scriptTargetId;
        saving = true; error = ""; Notify();
        try
        {
            await repository.UpdateScriptsAsync(owner, settings, targetId: targetId);
            if (projectId == owner && version == revision) Apply(settings);
        }
        finally { saving = false; Notify(); }
    }

    private void Apply(ProjectScriptSettings settings)
    {
        Scripts.Clear(); foreach (var script in settings.Scripts) Scripts.Add(script);
        selectedId = settings.SelectedId;
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(Label)); OnPropertyChanged(nameof(CanUse));
        OnPropertyChanged(nameof(ActionHint)); OnPropertyChanged(nameof(ActionGlyph));
        OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(HasError));
    }
}
