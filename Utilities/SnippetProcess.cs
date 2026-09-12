using System.Diagnostics;
using System.Text;

namespace PiAgentGui.Utilities;

/// <summary>Builds direct interpreter launches, without routing Windows Bash requests into WSL.</summary>
public static class SnippetProcess
{
    public static ProcessStartInfo Create(string directory, string label, string file, IReadOnlyList<string>? searchDirectories = null)
    {
        var language = SnippetLanguage.Normalize(label);
        var candidates = language switch
        {
            "powershell" => new[] { "pwsh.exe", "powershell.exe" },
            "python" => new[] { "python.exe", "python3.exe", "py.exe" },
            "bash" => new[] { "bash.exe" },
            _ => throw new ArgumentException("Unsupported interpreter.")
        };
        var directories = searchDirectories ?? (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        var executable = candidates.SelectMany(name => directories.Select(folder => Path.Combine(folder.Trim('"'), name)))
            .FirstOrDefault(path => Path.IsPathFullyQualified(path) && File.Exists(path)
                && !path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)
                && !(language == "bash" && path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
        if (executable is null) throw new IOException($"No {language} interpreter found on this Windows target. Install it or use a conversation on a target where it is available.");
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        if (language == "powershell")
        {
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-OutputFormat", "Text", "-EncodedCommand" }) start.ArgumentList.Add(arg);
            var invoke = "[Console]::OutputEncoding = $OutputEncoding = [System.Text.UTF8Encoding]::new($false); & '" + file.Replace("'", "''") + "'";
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(invoke)));
            return start;
        }
        if (language == "python")
        {
            if (Path.GetFileName(executable).Equals("py.exe", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add("-3");
            start.ArgumentList.Add("-u");
            start.Environment["PYTHONIOENCODING"] = "utf-8";
        }
        if (language == "bash") start.ArgumentList.Add("--");
        start.ArgumentList.Add(file);
        return start;
    }
}
