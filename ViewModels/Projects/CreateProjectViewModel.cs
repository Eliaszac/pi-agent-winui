using PiAgentGui.Models.Projects;
using PiAgentGui.Services.Projects;
using PiAgentGui.Utilities;

namespace PiAgentGui.ViewModels.Projects;

/// <summary>Owns the modal form and its asynchronous save state.</summary>
public sealed class CreateProjectViewModel(ProjectService service) : ObservableObject
{
    private readonly ProjectService service = service ?? throw new ArgumentNullException(nameof(service));
    private string name = "";
    private string path = "";
    private string errorMessage = "";
    private bool isBusy;

    /// <summary>Gets or sets the user-entered name.</summary>
    public string Name { get => name; set { if (SetProperty(ref name, value)) OnPropertyChanged(nameof(CanSubmit)); } }
    /// <summary>Gets or sets the selected absolute directory.</summary>
    public string Path { get => path; set { if (SetProperty(ref path, value)) OnPropertyChanged(nameof(CanSubmit)); } }
    /// <summary>Gets or sets recoverable form feedback.</summary>
    public string ErrorMessage { get => errorMessage; set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    /// <summary>Gets whether an error is visible.</summary>
    public bool HasError => ErrorMessage.Length > 0;
    /// <summary>Gets whether a save is pending.</summary>
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value)) return;
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(IsEditable));
        }
    }
    /// <summary>Gets whether fields can be edited.</summary>
    public bool IsEditable => !IsBusy;
    /// <summary>Gets whether the form is ready to submit.</summary>
    public bool CanSubmit => !IsBusy && !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Path);

    /// <summary>Attempts to save the form, preserving input on failure.</summary>
    /// <returns>The saved project, or null when validation or persistence fails.</returns>
    public async Task<Project?> TryCreateAsync()
    {
        if (!CanSubmit) return null;
        var savedName = Name;
        var savedPath = Path;
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            // Directory validation and lock acquisition may block on an unavailable drive.
            return await Task.Run(() => service.CreateAsync(savedName, savedPath));
        }
        catch (Exception exception)
        {
            ErrorMessage = ProjectErrorMessage.From(exception);
            return null;
        }
        finally { IsBusy = false; }
    }
}
