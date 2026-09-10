using System.Windows.Input;

namespace PiAgentGui.Utilities;

/// <summary>Runs an asynchronous UI command once at a time and reports failures.</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> execute;
    private readonly Action<Exception> onError;
    private bool isRunning;

    /// <summary>Creates a command with an explicit error boundary.</summary>
    /// <param name="execute">The asynchronous presentation action.</param>
    /// <param name="onError">The UI error handler.</param>
    public AsyncRelayCommand(Func<object?, Task> execute, Action<Exception> onError)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.onError = onError ?? throw new ArgumentNullException(nameof(onError));
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;
    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !isRunning;

    /// <inheritdoc />
    public async void Execute(object? parameter) => await ExecuteAsync(parameter);

    /// <summary>Runs the command with the same execution guard and error handling as the UI.</summary>
    /// <param name="parameter">The optional command argument.</param>
    public async Task ExecuteAsync(object? parameter = null)
    {
        if (isRunning) return;
        isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(parameter); }
        catch (Exception exception) { onError(exception); }
        finally
        {
            isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
