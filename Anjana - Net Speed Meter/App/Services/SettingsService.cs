using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Anjana.Services;

public enum SpeedUnit { Bytes, Bits }

public sealed class AppSettings
{
    public SpeedUnit Unit { get; set; } = SpeedUnit.Bytes;
    public bool LockPosition { get; set; } = false;
    /// <summary>Horizontal nudge (DIPs) relative to the default slot beside the system tray.</summary>
    public double OffsetDip { get; set; } = 0;
}

public sealed class SettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Anjana");
    private static string FilePath => Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Current { get; private set; } = new();
    public event Action? Changed;

    public static SettingsService Load()
    {
        var svc = new SettingsService();
        try
        {
            if (File.Exists(FilePath))
                svc.Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch { /* corrupt file → defaults */ }
        return svc;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, Json));
        }
        catch { /* non-fatal */ }
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Save();
        Changed?.Invoke();
    }
}

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.Name;

    public static bool IsEnabled
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(ValueName) is string; }
    }

    public static void Set(bool enabled)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (k is null) return;
        if (enabled && Environment.ProcessPath is { } path) k.SetValue(ValueName, $"\"{path}\"");
        else k.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
