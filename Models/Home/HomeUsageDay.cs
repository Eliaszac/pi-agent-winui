namespace PiAgentGui.Models.Home;

public sealed record HomeUsageDay(DateOnly Date, decimal Tokens, int Responses, double Height)
{
    public string Description => $"{Date:MMM d}: {Tokens:N0} reported tokens · {Responses:N0} responses";
}
