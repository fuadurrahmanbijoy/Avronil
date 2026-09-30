using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Anjana.Services;

/// <summary>Follows the Windows light/dark setting for the taskbar and swaps brush tokens at runtime.</summary>
public static class ThemeService
{
    public static bool IsLight { get; private set; }
    public static event Action? Changed;

    // token → (light, dark) — values follow Windows 11 Fluent text/fill ramps
    private static readonly Dictionary<string, (string Light, string Dark)> Tokens = new()
    {
        ["TextPrimary"]         = ("#E4000000", "#FFFFFFFF"),
        ["TextSecondary"]       = ("#9E000000", "#C8FFFFFF"),
        ["HoverFill"]           = ("#0F000000", "#14FFFFFF"),
        ["CardFill"]            = ("#B3FFFFFF", "#0DFFFFFF"),
        ["CardStroke"]          = ("#0F000000", "#19FFFFFF"),
        ["Divider"]             = ("#14000000", "#15FFFFFF"),
        ["ControlFillSelected"] = ("#FFFFFFFF", "#2EFFFFFF"),
        ["AccentFill"]          = ("#FF005FB8", "#FF60CDFF"),
        ["AccentText"]          = ("#FFFFFFFF", "#FF000000"),
        ["DownBrush"]           = ("#FF005FB8", "#FF60CDFF"),
        ["UpBrush"]             = ("#FF0F7B0F", "#FF6CCB5F"),
        ["SurfaceSolid"]        = ("#FFF3F3F3", "#FF2C2C2C"),
    };

    public static void Initialize()
    {
        Apply(ReadIsLight());
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;
            Task.Delay(300).ContinueWith(_ => Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                bool light = ReadIsLight();
                if (light != IsLight) Apply(light);
            }));
        };
    }

    private static bool ReadIsLight()
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return k?.GetValue("SystemUsesLightTheme") is int v && v == 1;
    }

    private static void Apply(bool light)
    {
        IsLight = light;
        var res = Application.Current.Resources;
        foreach (var (key, (l, d)) in Tokens)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(light ? l : d)!;
            brush.Freeze();
            res[key] = brush;
        }
        Changed?.Invoke();
    }
}
