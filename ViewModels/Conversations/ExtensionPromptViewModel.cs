using System.Text.Json.Nodes;
using PiAgentGui.Models.Conversations;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Conversations;

/// <summary>An explicit question owned by one conversation; never automatically approved.</summary>
public sealed class ExtensionPromptViewModel : ObservableObject, IDisposable
{
    private readonly ExtensionPrompt prompt;
    private readonly CancellationTokenSource lifetime = new();
    private string value;
    private bool pending = true;
    internal string Id => prompt.Id;
    public string Title => prompt.Title;
    public string Message => prompt.Message;
    public IReadOnlyList<string> Options => prompt.Options;
    public bool IsConfirm => prompt.Method == "confirm";
    public bool IsSelect => prompt.Method == "select";
    public bool IsText => prompt.Method is "input" or "editor";
    public bool IsPending { get => pending; private set => SetProperty(ref pending, value); }
    public string Value { get => value; set => SetProperty(ref this.value, value); }
    public string SubmitLabel => IsConfirm ? "Confirm" : "Submit";
    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public ExtensionPromptViewModel(ExtensionPrompt prompt, Func<string, JsonObject, Task> reply,
        Action<ExtensionPromptViewModel> remove, Action<Exception> reportError)
    {
        this.prompt = prompt;
        value = prompt.InitialValue;
        SubmitCommand = new AsyncRelayCommand(_ => RespondAsync(false), reportError);
        CancelCommand = new AsyncRelayCommand(_ => RespondAsync(true), reportError);

        async Task RespondAsync(bool cancel)
        {
            if (!IsPending || (!cancel && IsSelect && !Options.Contains(Value))) return;
            IsPending = false;
            try
            {
                var response = IsConfirm ? new JsonObject { ["confirmed"] = !cancel }
                    : cancel ? new JsonObject { ["cancelled"] = true } : new JsonObject { ["value"] = Value };
                await reply(Id, response);
                remove(this);
            }
            catch { IsPending = true; throw; }
        }
    }

    internal async Task ExpireAsync(Action<Action> dispatch, Action<ExtensionPromptViewModel> remove)
    {
        if (prompt.TimeoutMilliseconds is not int milliseconds) return;
        try
        {
            await Task.Delay(milliseconds, lifetime.Token).ConfigureAwait(false);
            dispatch(() => { IsPending = false; remove(this); });
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() { IsPending = false; lifetime.Cancel(); lifetime.Dispose(); }
}
