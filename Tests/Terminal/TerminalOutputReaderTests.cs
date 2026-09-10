using System.IO.Pipes;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PiAgentGui.Services.Terminal;

namespace PiAgentGui.Tests.Terminal;

[TestClass]
public sealed class TerminalOutputReaderTests
{
    [TestMethod]
    public async Task PromptArrivesBeforePipeClosesAndSplitUtf8IsPreserved()
    {
        using var writer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unicode = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new StringBuilder();
        var reading = Task.Run(() => TerminalOutputReader.Read(reader, text =>
        {
            received.Append(text);
            if (received.ToString() == "PS> ") prompt.TrySetResult();
            if (received.ToString() == "PS> 😀") unicode.TrySetResult();
        }));
        try
        {
            writer.Write(Encoding.UTF8.GetBytes("PS> "));
            writer.Flush();
            await prompt.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var bytes = Encoding.UTF8.GetBytes("😀");
            foreach (var value in bytes) { writer.WriteByte(value); writer.Flush(); }
            await unicode.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            writer.Dispose();
            await reading.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.AreEqual("PS> 😀", received.ToString());
    }
}
