using System.Diagnostics;
using PiAgentGui.Services.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>Conversation-owned snippet state; view changes do not terminate a run or discard its result.</summary>
public sealed class SnippetViewModel(ISnippetService service, IUiDispatcher dispatcher, string label, string code, Action<string> addToDraft) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private readonly object outputLock = new();
    private string pendingOutput = "";
    private string output = "", status = "";
    private string runStatus = "";
    private bool busy, running, truncated;
    public string Target => service.TargetLabel;
    public bool CanRun => !busy && SnippetLanguage.CanRun(label);
    public bool SupportsRun => SnippetLanguage.CanRun(label);
    public bool CanSave => !busy;
    public bool IsRunning => running;
    public bool HasOutput => output.Length > 0;
    public bool CanDismiss => !busy && (HasOutput || status.Length > 0);
    public string Output => output;
    public string Status => status;
    public Task? ActiveOperation { get; private set; }

    public Task RunAsync() => !CanRun ? ActiveOperation ?? Task.CompletedTask : ActiveOperation = RunCoreAsync();
    private async Task RunCoreAsync()
    {
        if (!CanRun) return;
        busy = running = true; output = ""; pendingOutput = ""; truncated = false;
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var watch = Stopwatch.StartNew();
        status = "Running · 0s"; Notify();
        using var timer = new Timer(_ => dispatcher.Post(() =>
        {
            if (!running) return;
            FlushOutput();
            status = watch.Elapsed.TotalSeconds < 60 ? $"Running · {(int)watch.Elapsed.TotalSeconds}s" : $"Running · {(int)watch.Elapsed.TotalMinutes}m {watch.Elapsed.Seconds}s";
            Notify();
        }), null, 250, 250);
        try
        {
            var exit = await service.RunAsync(label, code, (text, error) =>
            {
                lock (outputLock)
                {
                    pendingOutput += text;
                    if (pendingOutput.Length > 131072) { pendingOutput = pendingOutput[^131072..]; truncated = true; }
                }
            }, operation.Token);
            status = $"Exit {exit} · {watch.Elapsed.TotalSeconds:F1}s";
        }
        catch (OperationCanceledException) { status = "Stopped. Changes already made are kept; a disconnected remote target may need inspection."; }
        catch (Exception error) { status = ConversationErrors.Describe(error.Message).Message; }
        finally { runStatus = status; FlushOutput(); busy = running = false; operation.Dispose(); operation = null; Notify(); }
    }

    public Task SaveAsync() => !CanSave ? ActiveOperation ?? Task.CompletedTask : ActiveOperation = SaveCoreAsync();
    private async Task SaveCoreAsync()
    {
        if (!CanSave) return;
        busy = true; status = "Saving to project…"; Notify();
        try { status = "Saved " + await service.SaveAsync(label, code, lifetime.Token); }
        catch (OperationCanceledException) { status = "Save interrupted. Check the workspace before retrying."; }
        catch (Exception error) { status = ConversationErrors.Describe(error.Message).Message; }
        finally { busy = false; Notify(); }
    }
    public void Stop() { operation?.Cancel(); }
    public void DismissOutput()
    {
        if (!CanDismiss) return;
        lock (outputLock) { output = pendingOutput = ""; truncated = false; }
        status = runStatus = "";
        Notify();
    }
    public void AddOutputToDraft() { if (!running && HasOutput) addToDraft($"Snippet result ({label}, {Target}):\n{runStatus}\n\n{Output}"); }
    public void Dispose() { lifetime.Cancel(); }
    private void FlushOutput()
    {
        lock (outputLock)
        {
            output += pendingOutput; pendingOutput = "";
            if (output.Length > 131072) { output = output[^131072..]; truncated = true; }
            if (truncated && !output.StartsWith("[Earlier output omitted]", StringComparison.Ordinal)) output = "[Earlier output omitted]\n" + output;
        }
    }
    private void Notify()
    {
        OnPropertyChanged(string.Empty);
    }
}
