using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace PiAgentGui.Services.Terminal;

/// <summary>Owns one interactive shell and its Windows pseudoconsole. Native I/O stays off the UI thread.</summary>
public sealed class ConPtySession(string directory) : ITerminalSession
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly Channel<string> input = Channel.CreateBounded<string>(new BoundedChannelOptions(128) { SingleReader = true });
    private readonly CancellationTokenSource lifetime = new();
    private FileStream? inputStream;
    private FileStream? outputStream;
    private nint console;
    private nint process;
    private Task? reader;
    private Task? writer;
    private Task? monitor;
    private volatile bool disposed;
    public event Action<string>? Output;
    public event Action? Exited;

    public async Task StartAsync(int columns, int rows)
    {
        await lifecycle.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (console != 0) return;
            await Task.Run(() => Start(columns, rows));
            reader = Task.Run(ReadOutput);
            writer = Task.Run(WriteInputAsync);
            monitor = Task.Run(() =>
            {
                ConPtyNative.WaitForSingleObject(process, uint.MaxValue);
                if (!disposed) Exited?.Invoke();
            });
        }
        finally { lifecycle.Release(); }
    }

    private void Start(int columns, int rows)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException();
        if (!ConPtyNative.CreatePipe(out var inputRead, out var inputWrite, 0, 0)) throw new Win32Exception();
        using (inputRead)
        {
            if (!ConPtyNative.CreatePipe(out var outputRead, out var outputWrite, 0, 0)) { inputWrite.Dispose(); throw new Win32Exception(); }
            using (outputWrite)
            {
                inputStream = new FileStream(inputWrite, FileAccess.Write, 4096, false);
                outputStream = new FileStream(outputRead, FileAccess.Read, 4096, false);
                nint attributes = 0;
                var initialized = false;
                try
                {
                    Marshal.ThrowExceptionForHR(ConPtyNative.CreatePseudoConsole(Size(columns, rows), inputRead, outputWrite, 0, out console));
                    nuint bytes = 0;
                    ConPtyNative.InitializeProcThreadAttributeList(0, 1, 0, ref bytes);
                    attributes = Marshal.AllocHGlobal(checked((int)bytes));
                    if (!ConPtyNative.InitializeProcThreadAttributeList(attributes, 1, 0, ref bytes)) throw new Win32Exception();
                    initialized = true;
                    if (!ConPtyNative.UpdateProcThreadAttribute(attributes, 0, 0x00020016, console, (nuint)nint.Size, 0, 0)) throw new Win32Exception();
                    var startup = new ConPtyNative.StartupInfoEx { Startup = new() { Size = Marshal.SizeOf<ConPtyNative.StartupInfoEx>() }, Attributes = attributes };
                    var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
                    if (!File.Exists(shell)) shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                    if (!ConPtyNative.CreateProcessW(shell, new StringBuilder($"\"{shell}\" -NoLogo"), 0, 0, false, 0x00080000, 0, directory, ref startup, out var child)) throw new Win32Exception();
                    process = child.Process;
                    ConPtyNative.CloseHandle(child.Thread);
                }
                catch
                {
                    // No child is reading/writing yet if creation failed.
                    outputStream.Dispose();
                    inputStream.Dispose();
                    if (console != 0) { ConPtyNative.ClosePseudoConsole(console); console = 0; }
                    throw;
                }
                finally
                {
                    if (initialized) ConPtyNative.DeleteProcThreadAttributeList(attributes);
                    if (attributes != 0) Marshal.FreeHGlobal(attributes);
                }
            }
        }
    }

    private void ReadOutput()
    {
        try
        {
            TerminalOutputReader.Read(outputStream!, text => { if (!disposed) Output?.Invoke(text); });
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException) { }
    }

    private async Task WriteInputAsync()
    {
        try
        {
            await foreach (var data in input.Reader.ReadAllAsync(lifetime.Token))
            {
                var bytes = Encoding.UTF8.GetBytes(data);
                inputStream!.Write(bytes);
                inputStream.Flush();
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException) { }
    }

    public async Task WriteAsync(string text)
    {
        if (!disposed && text.Length <= 65536) await input.Writer.WriteAsync(text, lifetime.Token);
    }

    public async Task ResizeAsync(int columns, int rows)
    {
        await lifecycle.WaitAsync();
        try { if (!disposed && console != 0) await Task.Run(() => Marshal.ThrowExceptionForHR(ConPtyNative.ResizePseudoConsole(console, Size(columns, rows)))); }
        finally { lifecycle.Release(); }
    }

    private static ConPtyNative.Coord Size(int columns, int rows) => new() { X = (short)Math.Clamp(columns, 2, 500), Y = (short)Math.Clamp(rows, 2, 300) };

    public async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync();
        try
        {
            if (disposed) return;
            disposed = true;
            input.Writer.TryComplete();
            lifetime.Cancel();
            // Keep the output reader draining while ConPTY emits its final frame and terminates attached children.
            if (console != 0) { await Task.Run(() => ConPtyNative.ClosePseudoConsole(console)); console = 0; }
            await Task.WhenAll(reader ?? Task.CompletedTask, writer ?? Task.CompletedTask, monitor ?? Task.CompletedTask);
            inputStream?.Dispose();
            outputStream?.Dispose();
            if (process != 0) { ConPtyNative.CloseHandle(process); process = 0; }
        }
        finally { lifecycle.Release(); }
    }
}
