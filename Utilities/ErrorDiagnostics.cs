using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PiAgentGui.Configuration;

namespace PiAgentGui.Utilities;

/// <summary>Builds shareable metadata without exception messages, source paths, or protocol payloads.</summary>
public static class ErrorDiagnostics
{
    public static string Create(Exception? exception = null)
    {
        var report = new StringBuilder();
        report.AppendLine($"Report: {Guid.NewGuid():N}");
        report.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        report.AppendLine($"App: {ApplicationIdentity.Name} {typeof(ApplicationIdentity).Assembly.GetName().Version}");
        report.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"OS: {Environment.OSVersion.Version} · {RuntimeInformation.ProcessArchitecture}");
        if (exception is null) report.AppendLine("Source: Pi-reported error (raw response omitted)");
        for (var current = exception; current is not null; current = current.InnerException)
        {
            report.AppendLine($"Exception: {current.GetType().FullName} · HRESULT: 0x{current.HResult:X8}");
            if (current is Services.Pi.PiProcessExitException exit) report.AppendLine($"Pi exit code: {exit.ExitCode?.ToString() ?? "unavailable"}");
            foreach (var frame in new StackTrace(current, false).GetFrames().Take(15))
                if (frame.GetMethod() is { } method) report.AppendLine($"  {method.DeclaringType?.FullName}.{method.Name}");
        }
        report.AppendLine("Prompts, provider responses, credentials, file paths, and session identifiers are not included.");
        return report.ToString();
    }
}
