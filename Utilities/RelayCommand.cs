using System.Windows.Input;

namespace PiAgentGui.Utilities;

/// <summary>Adapts a presentation action to an ICommand.</summary>
public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    private readonly Action<object?> execute = execute ?? throw new ArgumentNullException(nameof(execute));

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;
    /// <inheritdoc />
    public void Execute(object? parameter) => execute(parameter);
}
