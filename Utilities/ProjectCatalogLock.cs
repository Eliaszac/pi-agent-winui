using System.Diagnostics;

namespace PiAgentGui.Utilities;

/// <summary>Waits briefly for exclusive catalog access without blocking the UI thread.</summary>
public static class ProjectCatalogLock
{
    public static async Task<FileStream> AcquireAsync(string catalogPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Retain the file: deleting it would race with other processes opening the same lock.
                return new FileStream(catalogPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when ((exception.HResult & 0xffff) is 32 or 33)
            {
                if (elapsed.Elapsed >= TimeSpan.FromSeconds(5))
                    throw new IOException("The project catalog is busy. Wait a moment and try again, or close other Pi desktop windows if this continues.", exception);
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
