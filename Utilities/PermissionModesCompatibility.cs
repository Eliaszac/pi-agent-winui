using System.Text.RegularExpressions;

namespace PiAgentGui.Utilities;

/// <summary>The narrowly scoped Codex request compatibility repair for permission-modes 2.6.3.</summary>
public static class PermissionModesCompatibility
{
    public const string FixedOptions = "...(model.api === \"openai-codex-responses\" ? {} : { temperature: 0 }),";
    private const string OriginalPattern = @"(?m)^([ \t]*)temperature: 0,(?=\r?$)";

    public static bool IsInstalled(string packageDirectory)
    {
        var file = Path.Combine(packageDirectory, "classifier-client.ts");
        if (!File.Exists(file)) return false;
        var source = File.ReadAllText(file);
        return source.Contains(FixedOptions, StringComparison.Ordinal) && !Regex.IsMatch(source, OriginalPattern);
    }

    // A self-contained command avoids referring to a developer's checkout or an installer-specific path.
    public static string SetupCommand => $$"""
        & {
            $ErrorActionPreference = 'Stop'
            pi install npm:{{PermissionModesSupport.Package}}@{{PermissionModesSupport.Version}}
            if ($LASTEXITCODE -ne 0) { throw 'Pi installation failed. Compatibility repair was not run.' }
            $piProfile = $env:USERPROFILE
            if ([string]::IsNullOrWhiteSpace($piProfile)) { $piProfile = [Environment]::GetFolderPath('UserProfile') }
            $piRoot = $env:PI_CODING_AGENT_DIR
            if ([string]::IsNullOrEmpty($piRoot)) { $piRoot = Join-Path $piProfile '.pi/agent' }
            elseif ($piRoot -eq '~') { $piRoot = $piProfile }
            elseif ($piRoot.StartsWith('~/') -or $piRoot.StartsWith('~\')) { $piRoot = Join-Path $piProfile $piRoot.Substring(2) }
            $piPackage = Join-Path ([IO.Path]::GetFullPath($piRoot)) 'npm/node_modules/@georgedong32/permission-modes'
            $piManifest = Get-Content -LiteralPath (Join-Path $piPackage 'package.json') -Raw | ConvertFrom-Json
            if ($piManifest.name -ne '{{PermissionModesSupport.Package}}' -or $piManifest.version -ne '{{PermissionModesSupport.Version}}') {
                throw 'Unexpected permission-modes package. No source was modified.'
            }
            $piFile = Join-Path $piPackage 'classifier-client.ts'
            $piSource = [IO.File]::ReadAllText($piFile)
            $piFixed = '{{FixedOptions}}'
            $piPattern = '{{OriginalPattern}}'
            if (-not ($piSource.Contains($piFixed) -and -not [regex]::IsMatch($piSource, $piPattern))) {
                if ($piSource.Contains($piFixed) -or [regex]::Matches($piSource, $piPattern).Count -ne 1 -or -not $piSource.Contains('buildClassifierCompletionOptions')) {
                    throw 'Unexpected classifier source. No source was modified.'
                }
                $piUpdated = [regex]::Replace($piSource, $piPattern, '${1}' + $piFixed)
                $piBackup = $piFile + '.before-pi-gui-codex-fix'
                if (-not (Test-Path -LiteralPath $piBackup)) { [IO.File]::Copy($piFile, $piBackup) }
                $piTemporary = $piFile + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
                try {
                    [IO.File]::WriteAllText($piTemporary, $piUpdated, [Text.UTF8Encoding]::new($false))
                    Move-Item -LiteralPath $piTemporary -Destination $piFile -Force
                } finally { if (Test-Path -LiteralPath $piTemporary) { Remove-Item -LiteralPath $piTemporary } }
            }
            Write-Host 'Permission Modes compatibility ready. Configure a connected classifier model, then restart Pi desktop.'
        }
        """;
}
