namespace PiAgentGui.Models.Settings;

public sealed record StorageUsage(string Name, long Bytes, bool Incomplete)
{
    public string Size => (Incomplete ? "At least " : "") + (Bytes < 1024 ? $"{Bytes} B" : Bytes < 1024 * 1024 ? $"{Bytes / 1024d:0.#} KB" : Bytes < 1024L * 1024 * 1024 ? $"{Bytes / (1024d * 1024):0.#} MB" : $"{Bytes / (1024d * 1024 * 1024):0.##} GB");
}
