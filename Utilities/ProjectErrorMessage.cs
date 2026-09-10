namespace PiAgentGui.Utilities;

/// <summary>Maps persistence errors to actionable UI copy without exposing internal details.</summary>
public static class ProjectErrorMessage
{
    /// <summary>Formats an error for a project operation.</summary>
    /// <param name="exception">The operation failure.</param>
    /// <returns>A user-facing message.</returns>
    public static string From(Exception exception) => exception switch
    {
        InvalidDataException => "The saved project list could not be read. Your file has been preserved. Restore a valid copy and try again.",
        DirectoryNotFoundException => "This folder is unavailable. Choose an existing folder you can access.",
        UnauthorizedAccessException => "Access was denied. Check the folder permissions and try again.",
        IOException => "The project list could not be accessed. Another window may be saving changes. Try again.",
        InvalidOperationException => "This folder is already registered. Open its project from the sidebar.",
        KeyNotFoundException => "This project is no longer available. Reload the project list and try again.",
        ArgumentException => "Enter a project name and choose a valid, absolute folder path.",
        _ => "Something went wrong. Try again."
    };
}
