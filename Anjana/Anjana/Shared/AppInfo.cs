using System;
using System.IO;

namespace Anjana;

/// <summary>Single source of truth shared by the app and the installer.</summary>
public static class AppInfo
{
    public const string Name = "Anjana";
    public const string Publisher = "Bijoy";
    public const string Tagline = "Built with love and care";
    public const string Version = "1.0.0";
    public const string ExeName = "Anjana.exe";

    public const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Anjana";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string DefaultInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Name);

    public static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), Name + ".lnk");

    public static string DesktopShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Name + ".lnk");
}
