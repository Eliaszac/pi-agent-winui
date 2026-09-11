[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = Join-Path $projectRoot 'artifacts/installer'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PiAgentGui.Desktop_is1'

if (Get-Process -Name PiAgentGui -ErrorAction SilentlyContinue) {
    throw 'Finish active runs and close Pi Agent, including development windows, then run this script again.'
}

$installed = if (Test-Path -LiteralPath $uninstallKey) { Get-ItemProperty -LiteralPath $uninstallKey } else { $null }
$installedVersion = if ($installed) { [version]$installed.DisplayVersion } else { $null }
if (-not $Version) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'PiAgentGui.csproj') -Raw
    $projectVersion = [version]$project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $Version = if ($installedVersion -and $installedVersion -ge $projectVersion) {
        '{0}.{1}.{2}' -f $installedVersion.Major, $installedVersion.Minor, ($installedVersion.Build + 1)
    } else { $projectVersion.ToString(3) }
}
if ($installedVersion -and [version]$Version -lt $installedVersion) {
    throw "Version $Version is older than installed version $installedVersion. Downgrades are not supported."
}
if (([version]$Version).Major -gt 65535 -or ([version]$Version).Minor -gt 65535 -or ([version]$Version).Build -gt 65535) {
    throw 'Each version component must be between 0 and 65535. Specify a new version with -Version.'
}

Write-Host "Building Pi Agent $Version for local installation..."
$buildParameters = @{ Version = $Version }
if ($CompilerPath) { $buildParameters.CompilerPath = $CompilerPath }
& (Join-Path $PSScriptRoot 'Build-Installer.ps1') @buildParameters

$installer = Join-Path $outputRoot "PiAgent-Setup-$Version-x64.exe"
$expectedHash = ((Get-Content -LiteralPath "$installer.sha256" -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'The installer checksum does not match. Installation was not started.'
}
if (Get-Process -Name PiAgentGui -ErrorAction SilentlyContinue) {
    throw "The installer was built, but Pi Agent is now running. Close it and run $installer to install."
}

$dataHashes = @{}
$dataDirectory = Join-Path $env:LOCALAPPDATA 'PiAgentGui'
if (Test-Path -LiteralPath $dataDirectory) {
    foreach ($dataFile in Get-ChildItem -LiteralPath $dataDirectory -Filter '*.json' -File) {
        $dataHashes[$dataFile.FullName] = (Get-FileHash -LiteralPath $dataFile.FullName -Algorithm SHA256).Hash
    }
}
$installLog = Join-Path $outputRoot ("install-$Version-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Write-Host "Installing Pi Agent $Version..."
$setup = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
    '/NOCLOSEAPPLICATIONS', '/SP-', ('/LOG="' + $installLog + '"')) -WindowStyle Hidden -Wait -PassThru
if ($setup.ExitCode -ne 0) {
    throw "Installation returned exit code $($setup.ExitCode). See $installLog"
}

$installed = Get-ItemProperty -LiteralPath $uninstallKey
$executable = Join-Path $installed.InstallLocation 'PiAgentGui.exe'
$binaryVersion = ((Get-Item -LiteralPath $executable).VersionInfo.ProductVersion -split '\+')[0]
if ([version]$installed.DisplayVersion -ne [version]$Version -or [version]$binaryVersion -ne [version]$Version) {
    throw "Installed version verification failed. See $installLog"
}
foreach ($dataPath in $dataHashes.Keys) {
    if (-not (Test-Path -LiteralPath $dataPath) -or (Get-FileHash -LiteralPath $dataPath -Algorithm SHA256).Hash -ne $dataHashes[$dataPath]) {
        throw "Installation finished, but application data changed: $([IO.Path]::GetFileName($dataPath)). See $installLog"
    }
}
Write-Host "Installed Pi Agent $Version successfully. Verified $($dataHashes.Count) unchanged application data files."
Write-Host "Application: $executable"
Write-Host "Installer: $installer"
Write-Host "Log: $installLog"
Write-Host 'Open Pi Agent from Start when ready to test.'
