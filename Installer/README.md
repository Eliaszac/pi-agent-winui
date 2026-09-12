# Local Windows installer

The first distribution target is Windows x64 (Windows 10 build 19041 or later). The application remains unpackaged. Inno Setup wraps a self-contained, untrimmed Release publish, including .NET and Windows App SDK. Reflection-based XAML bindings and grammar assets must not be trimmed.

## Build

For the complete local build-and-install workflow, close Pi desktop and run this from the repository in PowerShell as your normal Windows user:

```powershell
./Installer/Install-Local.ps1
```

The script builds the current workspace, including uncommitted changes, and installs a Release build. It automatically increments the installed patch version, or uses the project version if newer. This stamps the build without editing the project's version. Optional `-Version 0.2.0` selects an explicit version (same-version repair is allowed; downgrade is rejected); `-CompilerPath` overrides the Inno Setup compiler location.

It checks for running app windows before building and installing, verifies the installer checksum and installed version, and checks that existing top-level application JSON files are unchanged. It does not force-close the app, restart Windows, or launch the app afterward. Logs and generated files stay under ignored `artifacts/installer/`. The build prerequisites below still apply.

Install the .NET 10 SDK, Windows build tooling used by the project, and Inno Setup 6.7.3 from https://jrsoftware.org/isdl.php. Then run from the repository:

```powershell
./Installer/Build-Installer.ps1 -Version 0.1.1
```

Use `-CompilerPath 'C:\path\ISCC.exe'` for a custom compiler location. The script also searches `artifacts/installer/tools/inno/` for a local compiler. Generated tooling, prerequisites, publish directories, installers, hashes and logs live under ignored `artifacts/installer/`. Each publish uses a fresh directory to avoid accidentally packaging obsolete files from previous builds.

The resulting file is `artifacts/installer/PiDesktop-Setup-0.1.1-x64.exe` with an adjacent SHA-256 checksum. The version parameter stamps both application and installer. Use an increasing three-part numeric version for each release. Dependencies are pinned to the baseline already restored in this project.

## Install and update

- Run the installer normally, without administrator elevation. It installs for the current user under `%LOCALAPPDATA%\Programs\Pi Agent`.
- A stable `AppId=PiAgentGui.Desktop` ensures subsequent installers update the same installation and Windows Installed apps entry. Do not change this identity for subsequent releases.
- Newer versions replace application files in place. The same version can be installed again to repair its files. Older versions are rejected using the installed version registry entry.
- Setup uses Windows Restart Manager for files in use; finish active runs and close Pi desktop before installing. It does not automatically relaunch processes after an update. Interactive setup offers an optional launch at the end; silent setup never launches the app.
- A Start menu shortcut is installed; a desktop shortcut is optional and its selection is retained for upgrades.
- Shortcuts and installer labels use Pi desktop. Setup removes the legacy Pi Agent Start menu and desktop shortcuts; the existing installation directory and AppId remain unchanged for upgrade compatibility.
- Uninstall through Windows Installed apps. The uninstaller removes tracked application files and shortcuts, preserving `%LOCALAPPDATA%\PiAgentGui` and all Pi configuration, extensions, sessions and credentials in the user's Pi directory. It never uninstalls Pi or the shared WebView2 runtime.
- Keep application-owned runtime data outside the installation directory. If a future release removes or renames shipped files, add explicit obsolete-file cleanup to the installer for those known paths; ordinary file replacement does not remove files absent from a newer payload.

## Prerequisites

The installer includes Microsoft's signed Evergreen WebView2 bootstrapper. It checks both per-machine and per-user registration and runs it only if missing. Internet access is needed in that case; failure stops installation with an explanation. The build script verifies the bootstrapper's Microsoft Authenticode signature before bundling it. Existing WebView2 is shared and maintained by Microsoft.

Pi and its model/provider configuration remain separate. The app's existing Pi installation screen handles missing Pi. The installer does not silently install Pi, extensions, or modify provider authentication.

## Local testing versus public distribution

This local installer is unsigned. Public distribution still needs a code-signing identity for both application and installer, release hosting, and a separately designed authenticated update-check/download flow. No automatic updater or release publishing is enabled by this work. Re-running a newer local installer is the current update mechanism.

Verify fresh installation, same-version repair, upgrade, rejected downgrade, launch from the installed directory, uninstall and reinstall. Confirm user data is unchanged. A machine without .NET/Windows App SDK/WebView2 is needed to fully test first-time prerequisite installation; a developer machine cannot prove that scenario.

References:
- https://jrsoftware.org/ishelp/topic_setup_appid.htm
- https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps
- https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution
