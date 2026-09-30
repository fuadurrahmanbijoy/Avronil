using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Anjana.Setup;

public sealed record InstallOptions(string Dir, bool StartWithWindows, bool StartMenu, bool Desktop);

public static class Installer
{
    public static void Run(InstallOptions o, IProgress<(double Percent, string Status)> progress)
    {
        progress.Report((0, "Preparing…"));
        StopRunningInstances();
        Directory.CreateDirectory(o.Dir);

        string exe = Path.Combine(o.Dir, AppInfo.ExeName);
        CopyPayload(exe, progress);

        progress.Report((92, "Registering with Windows…"));
        RegisterUninstall(o.Dir, exe);

        progress.Report((95, "Adding shortcuts…"));
        Shortcut(AppInfo.StartMenuShortcut, o.StartMenu, exe, o.Dir);
        Shortcut(AppInfo.DesktopShortcut, o.Desktop, exe, o.Dir);

        progress.Report((98, "Finishing up…"));
        SetStartWithWindows(o.StartWithWindows, exe);

        progress.Report((100, "Done"));
    }

    private static void StopRunningInstances()
    {
        foreach (var p in Process.GetProcessesByName(AppInfo.Name))
        {
            try { p.Kill(); p.WaitForExit(4000); } catch { }
        }
    }

    private static void CopyPayload(string target, IProgress<(double Percent, string Status)> progress)
    {
        using var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe")
            ?? throw new InvalidOperationException("Installer payload is missing. Rebuild with build.ps1.");

        string tmp = target + ".tmp";
        long total = src.Length, done = 0;
        var buffer = new byte[256 * 1024];

        using (var dst = File.Create(tmp))
        {
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dst.Write(buffer, 0, read);
                done += read;
                progress.Report((5 + 85.0 * done / total, $"Copying {AppInfo.Name}…"));
            }
        }

        for (int attempt = 0; ; attempt++)
        {
            try { File.Move(tmp, target, overwrite: true); break; }
            catch (IOException) when (attempt < 6) { Thread.Sleep(500); }   // exe may still be releasing
        }
    }

    private static void RegisterUninstall(string dir, string exe)
    {
        using var k = Registry.CurrentUser.CreateSubKey(AppInfo.UninstallKeyPath);
        k.SetValue("DisplayName", AppInfo.Name);
        k.SetValue("DisplayVersion", AppInfo.Version);
        k.SetValue("Publisher", AppInfo.Publisher);
        k.SetValue("Comments", AppInfo.Tagline);
        k.SetValue("InstallLocation", dir);
        k.SetValue("DisplayIcon", exe);
        k.SetValue("UninstallString", $"\"{exe}\" --uninstall");
        k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void SetStartWithWindows(bool enabled, string exe)
    {
        using var k = Registry.CurrentUser.CreateSubKey(AppInfo.RunKeyPath);
        if (enabled) k.SetValue(AppInfo.Name, $"\"{exe}\"");
        else k.DeleteValue(AppInfo.Name, false);
    }

    private static void Shortcut(string path, bool create, string exe, string dir)
    {
        try
        {
            if (!create) { if (File.Exists(path)) File.Delete(path); return; }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(path);
            link.TargetPath = exe;
            link.WorkingDirectory = dir;
            link.Description = AppInfo.Tagline;
            link.IconLocation = exe + ",0";
            link.Save();
        }
        catch { /* shortcuts are a convenience; never fail the install over them */ }
    }
}
