namespace PiAgentGui.Services.Terminal;

public interface ITerminalSession : IAsyncDisposable
{
    event Action<string>? Output;
    event Action? Exited;
    Task StartAsync(int columns, int rows);
    Task WriteAsync(string text);
    Task ResizeAsync(int columns, int rows);
}
