# Anjana
*Built with love and care* — by Bijoy

Live network speed in your Windows 11 taskbar, plus a minimal themed installer.

## Build the installer
Requires the .NET 10 SDK (build machine only). Double-click `Build-Installer.bat`
(or run `.\build.ps1`). Alternatively push to GitHub: the included workflow builds it on
GitHub's Windows runners and attaches `Anjana-Setup.exe` as a downloadable artifact.

Output: `dist\Anjana-Setup.exe` — a single file you can copy or share.

## What the installer does
- Installs per-user to `%LocalAppData%\Programs\Anjana` (or any folder you pick). No admin prompt.
- Bundles the .NET runtime, so there are no dependencies to install.
- Optional: start with Windows, Start menu shortcut, desktop shortcut.
- Registers in Settings → Apps (name, version, publisher, icon, size, uninstall).
- Reinstalling upgrades in place and closes a running copy first.
- Launches Anjana when finished.

## Uninstall
Settings → Apps → Anjana → Uninstall (runs `Anjana.exe --uninstall`). It removes the app, shortcuts,
startup entry, registry entry and settings.

## Layout
    App/      the taskbar speed monitor
    Setup/    the installer (embeds the published app)
    Shared/   name, tagline and paths used by both
    assets/   anjana.ico / anjana.png   (regenerate with tools/make_icon.py)
