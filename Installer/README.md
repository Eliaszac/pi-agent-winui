# Local Windows installer

The first distribution target is Windows x64 (Windows 10 build 19041 or later). The application remains unpackaged. Inno Setup wraps a self-contained, untrimmed Release publish, including .NET and Windows App SDK. Reflection-based XAML bindings and grammar assets must not be trimmed.

## Build

Install the .NET 10 SDK, Windows build tooling used by the project, and Inno Setup 6.7.3 from https://jrsoftware.org/isdl.php. Then run from the repository:

```powershell
./Installer/Build-Installer.ps1 -Version 0.1.1
```

Use `-CompilerPath 'C:\path\ISCC.exe'` for a custom compiler location. This machine's local compiler is under `artifacts/installer/tools/inno/`. The script locates it automatically. Generated tooling, prerequisites, publish directories, installers, hashes and logs live under ignored `artifacts/installer/`. Each publish uses a fresh directory to avoid accidentally packaging obsolete files from previous builds.

The resulting file is `artifacts/installer/PiAgent-Setup-0.1.1-x64.exe` with an adjacent SHA-256 checksum. The version parameter stamps both application and installer. Use an increasing three-part numeric version for each release. Dependencies are pinned to the baseline already restored in this project.

Version 0.1.1 adds exclusive research-store ownership across app windows. Close all older app builds before using it; pre-0.1.1 processes do not participate in the ownership protocol. Its two new regression tests cover competing coordinators and failed initialization recovery.

Local testing update on 2026-09-11: built the current workspace as version 0.1.2 and upgraded the installed 0.1.0 successfully (installer exit code 0). Both the installed executable and uninstall registration report 0.1.2. The two existing top-level application JSON files retained their SHA-256 hashes. Installation used silent mode without closing running applications or launching the app afterward. Installer and log: `artifacts/installer/PiAgent-Setup-0.1.2-x64.exe` and `artifacts/installer/install-0.1.2.log`. This is a Release build; the temporary Debug-only `/test-error` command is not included.

## Install and update

- Run the installer normally, without administrator elevation. It installs for the current user under `%LOCALAPPDATA%\Programs\Pi Agent`.
- A stable `AppId=PiAgentGui.Desktop` ensures subsequent installers update the same installation and Windows Installed apps entry. Do not change this identity for subsequent releases.
- Newer versions replace application files in place. The same version can be installed again to repair its files. Older versions are rejected using the installed version registry entry.
- Setup uses Windows Restart Manager for files in use; finish active runs and close Pi Agent before installing. It does not automatically relaunch processes after an update. Interactive setup offers an optional launch at the end; silent setup never launches the app.
- A Start menu shortcut is installed; a desktop shortcut is optional and its selection is retained for upgrades.
- Uninstall through Windows Installed apps. The uninstaller removes tracked application files and shortcuts, preserving `%LOCALAPPDATA%\PiAgentGui` and all Pi configuration, extensions, sessions and credentials in the user's Pi directory. It never uninstalls Pi or the shared WebView2 runtime.
- Keep application-owned runtime data outside the installation directory. If a future release removes or renames shipped files, add explicit obsolete-file cleanup to the installer for those known paths; ordinary file replacement does not remove files absent from a newer payload.

## Prerequisites

The installer includes Microsoft's signed Evergreen WebView2 bootstrapper. It checks both per-machine and per-user registration and runs it only if missing. Internet access is needed in that case; failure stops installation with an explanation. The build script verifies the bootstrapper's Microsoft Authenticode signature before bundling it. Existing WebView2 is shared and maintained by Microsoft.

Pi and its model/provider configuration remain separate. The app's existing Pi installation screen handles missing Pi. The installer does not silently install Pi, extensions, or modify provider authentication.

## Local testing versus public distribution

This local installer is unsigned. Public distribution still needs a code-signing identity for both application and installer, release hosting, and a separately designed authenticated update-check/download flow. No automatic updater or release publishing is enabled by this work. Re-running a newer local installer is the current update mechanism.

Verify fresh installation, same-version repair, upgrade, rejected downgrade, launch from the installed directory, uninstall and reinstall. Confirm user data is unchanged. A machine without .NET/Windows App SDK/WebView2 is needed to fully test first-time prerequisite installation; a developer machine cannot prove that scenario.

Local verification on 2026-09-10 passed fresh 0.1.0 installation and launch, same-version repair, uninstall, reinstall with a 0.0.9 test build, upgrade to 0.1.0, and rejection of the 0.0.9 installer afterward. The four existing app JSON data files checked retained their hashes. Version 0.1.0 remains installed. The 0.0.9 installer is a test artifact only. All 234 application unit tests passed. WebView2 was already installed, so its missing-runtime branch remains untested on a clean machine.

References:
- https://jrsoftware.org/ishelp/topic_setup_appid.htm
- https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps
- https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution
