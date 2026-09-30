using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Anjana;

/// <summary>Runs when Windows (Settings → Apps) launches "Anjana.exe --uninstall".</summary>
internal static class Uninstaller
{
    public static void Run()
    {
        string exe = Environment.ProcessPath!;
        string dir = Path.GetDirectoryName(exe)!;

        var answer = MessageBox.Show(
            $"Remove {AppInfo.Name} from this computer?\n\n{AppInfo.Tagline}.",
            $"Uninstall {AppInfo.Name}", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        foreach (var p in Process.GetProcessesByName(AppInfo.Name))
        {
            if (p.Id == Environment.ProcessId) continue;
            try { p.Kill(); p.WaitForExit(3000); } catch { }
        }

        try { using var run = Registry.CurrentUser.OpenSubKey(AppInfo.RunKeyPath, true); run?.DeleteValue(AppInfo.Name, false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(AppInfo.UninstallKeyPath, false); } catch { }
        TryDelete(AppInfo.StartMenuShortcut);
        TryDelete(AppInfo.DesktopShortcut);
        try
        {
            string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.Name);
            if (Directory.Exists(settings)) Directory.Delete(settings, true);
        }
        catch { }

        MessageBox.Show($"{AppInfo.Name} has been removed.\n\nThank you for trying it.",
            $"Uninstall {AppInfo.Name}", MessageBoxButton.OK, MessageBoxImage.Information);

        // The running exe can't delete itself: hand off to a hidden cmd that waits for us to exit.
        // Only our own exe is deleted, then the folder is removed only if it is empty.
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 4 > nul & del /f /q \"{exe}\" & rmdir \"{dir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
