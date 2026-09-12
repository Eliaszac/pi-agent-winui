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
    public string Heading { get; }
    public string Preview { get; }
    public bool HasPreview => Preview.Length > 0;
    public bool HasMessage => Message.Length > 0;
    public string Context { get; }
    public bool HasContext => Context.Length > 0;
    public string Message => prompt.Message;
    public IReadOnlyList<string> Options => prompt.Options;
    public bool IsConfirm => prompt.Method == "confirm";
    public bool IsSelect => prompt.Method == "select";
    public bool IsText => prompt.Method is "input" or "editor";
    public bool IsPending { get => pending; private set { if (SetProperty(ref pending, value)) OnPropertyChanged(nameof(CanSubmit)); } }
    public string Value { get => value; set { if (SetProperty(ref this.value, value)) OnPropertyChanged(nameof(CanSubmit)); } }
    public bool CanSubmit => IsPending && (!IsSelect || Options.Contains(Value));
    public string SubmitLabel => IsConfirm ? "Confirm" : "Submit";
    public AsyncRelayCommand SubmitCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public ExtensionPromptViewModel(ExtensionPrompt prompt, Func<string, JsonObject, Task> reply,
        Action<ExtensionPromptViewModel> remove, Action<Exception> reportError, string context = "")
    {
        this.prompt = prompt;
        (Heading, Preview) = ExtensionPromptPresentation.Split(prompt);
        Context = context;
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
