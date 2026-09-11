using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Files;

public sealed class ExplorerItem(string path, bool directory, int depth, bool expanded, bool linked = false) : ObservableObject
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path) is { Length: > 0 } name ? name : Path;
    public bool IsDirectory { get; } = directory;
    public bool IsLinked { get; } = linked;
    public bool IsRoot => Depth == 0;
    public int Depth { get; } = depth;
    public double IndentWidth => Depth * 14;
    private bool isExpanded = expanded;
    public bool IsExpanded
    {
        get => isExpanded;
        internal set { if (SetProperty(ref isExpanded, value)) { OnPropertyChanged(nameof(Chevron)); OnPropertyChanged(nameof(IconName)); } }
    }
    public string Chevron => IsExpanded ? "\uE70D" : "\uE76C";
    public bool CanExpand => IsDirectory && !IsLinked;
    public string IconName => FileTypeIcon.Name(Path, IsDirectory, IsExpanded);
    private bool editing;
    public bool IsEditing { get => editing; set { if (SetProperty(ref editing, value)) OnPropertyChanged(nameof(ShowName)); } }
    public bool ShowName => !editing;
    private string draft = System.IO.Path.GetFileName(path);
    public string Draft { get => draft; set => SetProperty(ref draft, value); }
    public string? CreateParent { get; init; }
}
