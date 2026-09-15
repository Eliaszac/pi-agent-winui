namespace PiAgentGui.Models.Conversations;

/// <summary>A typed change delivered from a conversation runtime to its presentation state.</summary>
public sealed record ConversationUpdate
{
    public Utilities.ConversationLoadTiming? LoadTiming { get; init; }
    public System.Text.Json.JsonElement? GitHubWriteRequest { get; init; }
    public System.Text.Json.JsonElement? BrowserRequest { get; init; }
    public System.Text.Json.JsonElement? ArtifactRequest { get; init; }
    public System.Text.Json.JsonElement? Checkpoint { get; init; }
    public McpStatusSnapshot? McpStatus { get; init; }
    public InstructionSnapshot? Instructions { get; init; }
    public IReadOnlyList<string>? SteeringQueue { get; init; }
    public IReadOnlyList<PendingPrompt>? RecoveredPrompts { get; init; }
    public string? ErrorDiagnostics { get; init; }
    public ChatEntry? Entry { get; init; }
    public IReadOnlyList<ChatEntry>? History { get; init; }
    public string? Status { get; init; }
    public bool? IsRunning { get; init; }
    public bool? IsCompacting { get; init; }
    public bool? IsConnected { get; init; }
    public string? Error { get; init; }
    public string? Warning { get; init; }
    public PiAgentGui.Models.Pi.PiModel? Model { get; init; }
    public bool HasModelUpdate { get; init; }
    public IReadOnlyList<PiAgentGui.Models.Pi.PiModel>? AvailableModels { get; init; }
    public ExtensionPrompt? Prompt { get; init; }
    public bool DismissPrompts { get; init; }
    public bool? ApprovalAvailable { get; init; }
    public bool HasApprovalModeUpdate { get; init; }
    public string? ApprovalMode { get; init; }
    public string? ApprovalStatus { get; init; }
    public IReadOnlyList<string>? ThinkingLevels { get; init; }
    public bool HasThinkingLevelUpdate { get; init; }
    public string? ThinkingLevel { get; init; }
    public bool TurnCompleted { get; init; }
    public RunUsage? RunUsage { get; init; }
    public string? SessionName { get; init; }
}
