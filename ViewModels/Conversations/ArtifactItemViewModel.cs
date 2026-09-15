using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

public sealed class ArtifactItemViewModel(ArtifactRecord record, ArtifactPanelViewModel owner) : ObservableObject
{
    public ArtifactPanelViewModel Owner { get; } = owner;
    public ArtifactRecord Record { get; private set; } = record;
    public Guid Id => Record.Id;
    public string Name => Record.Name;
    public bool Available => !Record.Deleted && Record.Version != Guid.Empty;
    public string Description => Record.Deleted ? "Deleted" : Record.Version == Guid.Empty ? "Artifact unavailable" :
        $"{(Path.GetExtension(Name).TrimStart('.').ToUpperInvariant() is { Length: > 0 } type ? type : "File")} · {Record.Size / 1024d:N1} KB · {(Record.Shared ? Record.Origin : "Not sent")}";
    public string Glyph => Record.MimeType.StartsWith("image/", StringComparison.Ordinal) ? "\uEB9F" : "\uE8A5";
    internal void Update(ArtifactRecord record)
    {
        if (Record == record) return;
        Record = record;
        foreach (var property in new[] { nameof(Name), nameof(Description), nameof(Glyph), nameof(Available) }) OnPropertyChanged(property);
    }
}
