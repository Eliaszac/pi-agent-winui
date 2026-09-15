using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace PiAgentGui.Utilities;

/// <summary>Temporary content-free loading diagnostics, written away from the UI thread.</summary>
public sealed class ConversationLoadTiming
{
    public static readonly AsyncLocal<ConversationLoadTiming?> Current = new();
    private static readonly Channel<string> lines = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
        { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private static readonly Task writer = Task.Run(WriteAsync);
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly string id = Guid.NewGuid().ToString("N")[..12];
    public void Mark(string stage, double? durationMs = null, int? count = null) => lines.Writer.TryWrite(JsonSerializer.Serialize(new
    {
        utc = DateTimeOffset.UtcNow, load = id, stage, elapsedMs = Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2), durationMs, count
    }));
    public IDisposable Measure(string stage) => new TimingScope(this, stage);
    private static async Task WriteAsync()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PiAgentGui", "diagnostics");
        var path = Path.Combine(directory, "conversation-loading.jsonl");
        await foreach (var line in lines.Reader.ReadAllAsync())
        {
            try
            {
                Directory.CreateDirectory(directory);
                if (File.Exists(path) && new FileInfo(path).Length >= 2 * 1024 * 1024) File.Move(path, path + ".previous", true);
                await File.AppendAllTextAsync(path, line + Environment.NewLine);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Diagnostics must never interfere with loading. */ }
        }
    }
}
