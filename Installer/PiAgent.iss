#ifndef AppVersion
  #error AppVersion must be supplied by Build-Installer.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by Build-Installer.ps1
#endif
#define AppIdentity "PiAgentGui.Desktop"

[Setup]
AppId={#AppIdentity}
AppName=Pi Agent
AppVersion={#AppVersion}
AppVerName=Pi Agent {#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Pi Agent
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=PiAgent-Setup-{#AppVersion}-x64
SetupIconFile=..\Assets\Pi.ico
UninstallDisplayIcon={app}\PiAgentGui.exe
UninstallDisplayName=Pi Agent
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\Assets\FileTypes\VisualStudioImageLibrary-EULA.rtf
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
Uninstallable=yes
AllowCancelDuringInstall=no

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "{#Bootstrapper}"; Flags: dontcopy

[Icons]
Name: "{userprograms}\Pi Agent"; Filename: "{app}\PiAgentGui.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\Pi Agent"; Filename: "{app}\PiAgentGui.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\PiAgentGui.exe"; Description: "Launch Pi Agent"; Flags: nowait postinstall skipifsilent

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppIdentity}_is1';
  WebViewKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function InitializeSetup(): Boolean;
var
  Installed: String;
  InstalledVersion, NewVersion: Int64;
begin
  Result := True;
  if RegQueryStringValue(HKCU64, UninstallKey, 'DisplayVersion', Installed) then
    if StrToVersion(Installed, InstalledVersion) and StrToVersion('{#AppVersion}', NewVersion) then
      if ComparePackedVersion(InstalledVersion, NewVersion) > 0 then begin
        MsgBox('A newer version of Pi Agent is already installed. Uninstall it first if you intend to downgrade. Your conversation data will be preserved.', mbError, MB_OK);
        Result := False;
      end;
end;

function HasWebView2(): Boolean;
var Version: String;
begin
  Result := (RegQueryStringValue(HKLM32, WebViewKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
  if not Result then
    Result := (RegQueryStringValue(HKCU, WebViewKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  Result := '';
  if not HasWebView2() then begin
    WizardForm.StatusLabel.Caption := 'Installing Microsoft Edge WebView2 Runtime...';
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
      Result := 'Could not start Microsoft WebView2 setup. Please install the WebView2 Runtime and retry.';
      exit;
    end;
    if not HasWebView2() then
      Result := 'Microsoft WebView2 Runtime installation did not complete. Check your internet connection, install the WebView2 Runtime, then retry. Exit code: ' + IntToStr(ExitCode);
  end;
end;
