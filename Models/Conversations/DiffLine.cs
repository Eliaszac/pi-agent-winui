namespace PiAgentGui.Models.Conversations;

public sealed record DiffLine(string Text, long? OldLine, long? NewLine, char Kind)
{
    public int HiddenStart { get; init; } = -1;
    public int HiddenCount { get; init; }
    public bool CanExpand => HiddenStart >= 0 && HiddenCount > 0;
    public bool IsCodeRow => !CanExpand;
    public IReadOnlyList<CodeToken>? OldTokens { get; init; }
    public IReadOnlyList<CodeToken>? NewTokens { get; init; }
    public IReadOnlyList<CodeToken>? SyntaxTokens => NewLine is not null ? NewTokens : OldTokens;
    public bool IsAdded => Kind == '+';
    public bool IsRemoved => Kind == '-';
    public string OldNumber => OldLine?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public string NewNumber => NewLine?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public string Number => OldNumber.Length > 0 ? OldNumber : NewNumber;
    public string Marker => IsAdded ? "+" : IsRemoved ? "−" : Kind == 'h' ? "⋯" : "";
}
