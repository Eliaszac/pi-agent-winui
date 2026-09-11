namespace PiAgentGui.Models.Conversations;

public sealed record FileDiffContent(string FileName, string Patch, string Description, string LeftLabel, string RightLabel, string Notice = "");
