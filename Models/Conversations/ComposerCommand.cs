namespace PiAgentGui.Models.Conversations;

/// <summary>A native action or a command discovered from the active Pi process.</summary>
public sealed record ComposerCommand(string Name, string Description, string Source, string Action = "")
{
    public string Label => "/" + Name;
    public string Origin => Source switch { "app" => "Pi Agent", "extension" => "Extension", "skill" => "Skill", "prompt" => "Template", _ => "Pi" };
}
