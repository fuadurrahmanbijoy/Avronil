using System.Runtime.InteropServices;

namespace Anjana.Interop;

public static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10,
                       SWP_SHOWWINDOW = 0x40, SWP_HIDEWINDOW = 0x80;
    private const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);

    public static bool SupportsBackdrop => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Keeps the widget out of Alt+Tab and prevents it from ever stealing focus.</summary>
    public static void MakeToolWindow(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    public static void PlaceTopmost(IntPtr hwnd, int x, int y, int w, int h) =>
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);

    public static void Move(IntPtr hwnd, int x, int y, int w, int h) =>
        SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);

    public static void HideNoActivate(IntPtr hwnd) =>
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW);

    /// <summary>Rounded corners + acrylic backdrop (Win11 22H2+) + matching light/dark tint.</summary>
    public static void ApplyFlyoutChrome(IntPtr hwnd, bool dark)
    {
        int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, 4);          // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
        int d = dark ? 1 : 0; DwmSetWindowAttribute(hwnd, 20, ref d, 4);        // DWMWA_USE_IMMERSIVE_DARK_MODE
        if (SupportsBackdrop)
        {
            int acrylic = 3; DwmSetWindowAttribute(hwnd, 38, ref acrylic, 4);   // DWMWA_SYSTEMBACKDROP_TYPE = TRANSIENT (acrylic)
            var m = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref m);
        }
    }

    public sealed record TaskbarInfo(RECT Bounds, RECT? Tray, RECT Monitor)
    {
        public bool IsVertical => Bounds.Height > Bounds.Width;
        public bool IsAutoHiddenAway => Bounds.Top >= Monitor.Bottom - 2 || Bounds.Bottom <= Monitor.Top + 2;
    }

    public static TaskbarInfo? GetTaskbar()
    {
        IntPtr tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var bounds)) return null;

        RECT? notify = null;
        IntPtr n = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (n != IntPtr.Zero && GetWindowRect(n, out var nr) && nr.Width > 0) notify = nr;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(tray, 2), ref mi);
        return new TaskbarInfo(bounds, notify, mi.rcMonitor);
    }

    /// <summary>True for exclusive-fullscreen games, video players and presentations.</summary>
    public static bool IsFullscreenAppActive()
    {
        if (SHQueryUserNotificationState(out int s) != 0) return false;
        return s is 2 or 3 or 4; // BUSY, RUNNING_D3D_FULL_SCREEN, PRESENTATION_MODE
    }
}
