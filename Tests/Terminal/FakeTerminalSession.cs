using PiAgentGui.Services.Terminal;

namespace PiAgentGui.Tests.Terminal;

public sealed class FakeTerminalSession : ITerminalSession
{
    public event Action<string>? Output { add { } remove { } }
    public event Action? Exited { add { } remove { } }
    public bool Disposed { get; private set; }
    public Task StartAsync(int columns, int rows) => Task.CompletedTask;
    public Task WriteAsync(string text) => Task.CompletedTask;
    public Task ResizeAsync(int columns, int rows) => Task.CompletedTask;
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}
