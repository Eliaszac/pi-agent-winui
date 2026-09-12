namespace PiAgentGui.Models.Home;

public sealed record HomeUsageGroup(string Name, string Detail, decimal Tokens, int Responses, double Share)
{
    public string Value => $"{Tokens:N0}";
    public string Caption => $"{Detail} · {Responses:N0} responses";
}
