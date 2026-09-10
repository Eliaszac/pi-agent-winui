using PiAgentGui.Services.Pi;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Startup;

public sealed class StartupViewModel(PiInstallationLocator locator) : ObservableObject
{
    private bool isChecking = true;
    private string message = "";
    public bool IsChecking => isChecking;
    public bool ShowInstall => !isChecking && message.Length > 0;
    public string Message => message;

    public async Task<bool> CheckAsync()
    {
        try { await Task.Run(locator.Resolve); return true; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            message = exception.Message;
            return false;
        }
        finally
        {
            isChecking = false;
            OnPropertyChanged(nameof(IsChecking));
            OnPropertyChanged(nameof(ShowInstall));
            OnPropertyChanged(nameof(Message));
        }
    }
}
