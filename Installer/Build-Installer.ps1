[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.1',
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = Join-Path $projectRoot 'artifacts/installer'
$publishPath = Join-Path $outputRoot ('publish/' + $Version + '-' + [guid]::NewGuid().ToString('N'))
$prerequisitePath = Join-Path $outputRoot 'prerequisites'
New-Item -ItemType Directory -Force $publishPath, $prerequisitePath | Out-Null
if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $outputRoot 'tools/inno/ISCC.exe'),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Install Inno Setup 6.7.3 from https://jrsoftware.org/isdl.php or supply -CompilerPath.'
}
$bootstrapper = Join-Path $prerequisitePath 'MicrosoftEdgeWebview2Setup.exe'
if (-not (Test-Path -LiteralPath $bootstrapper)) {
    Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrapper
}
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'WebView2 bootstrapper signature is not a valid Microsoft signature.'
}
Push-Location $projectRoot
try {
    & dotnet publish PiAgentGui.csproj -c Release -p:Platform=x64 -p:PublishProfile=LocalInstaller -p:Version=$Version -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
    foreach ($file in @('PiAgentGui.exe', 'Microsoft.UI.Xaml.dll', 'coreclr.dll', 'PiAgentGui.pri', 'WebView2Loader.dll', 'Microsoft.Web.WebView2.Core.dll', 'GitHubApp.json', 'Assets/Pi.ico', 'PiExtensions/research-worker.ts', 'PiExtensions/providers.ts')) {
        if (-not (Test-Path -LiteralPath (Join-Path $publishPath $file))) { throw "Published payload is missing $file" }
    }
    & $CompilerPath /Q "/DAppVersion=$Version" "/DPublishDir=$publishPath" "/DOutputDir=$outputRoot" "/DBootstrapper=$bootstrapper" (Join-Path $PSScriptRoot 'PiAgent.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $outputRoot "PiDesktop-Setup-$Version-x64.exe"
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath "$installer.sha256" -Encoding ascii
    Write-Output "Installer: $installer"
} finally { Pop-Location }
