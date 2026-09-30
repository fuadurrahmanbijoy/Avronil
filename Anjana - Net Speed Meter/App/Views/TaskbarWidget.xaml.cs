using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Anjana.Interop;
using Anjana.Services;

namespace Anjana.Views;

public partial class TaskbarWidget : Window
{
    private const double WidgetWidth = 106;   // keep in sync with XAML
    private const double DragThresholdPx = 4;

    private readonly SettingsService _settings;
    private readonly NetworkMonitor _monitor;
    private readonly FlyoutWindow _flyout;
    private readonly DispatcherTimer _layoutTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private IntPtr _hwnd;
    private NativeMethods.TaskbarInfo? _taskbar;
    private bool _pressed, _dragging;
    private int _pressScreenX;
    private double _pressOffset;

    public TaskbarWidget(SettingsService settings, NetworkMonitor monitor, FlyoutWindow flyout)
    {
        InitializeComponent();
        _settings = settings;
        _monitor = monitor;
        _flyout = flyout;

        _monitor.Updated += _ => Render();
        _settings.Changed += Render;
        _layoutTimer.Tick += (_, _) => Reposition();   // also re-asserts z-order above the taskbar
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeToolWindow(_hwnd);
        Render();
        Reposition();
        _layoutTimer.Start();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Reposition();
    }

    protected override void OnClosed(EventArgs e)
    {
        _layoutTimer.Stop();
        base.OnClosed(e);
    }

    // ───────────── content ─────────────
    private void Render()
    {
        var s = _monitor.Current;
        var unit = _settings.Current.Unit;

        var (uv, uu) = SpeedFormatter.Rate(s.UpBps, unit);
        var (dv, du) = SpeedFormatter.Rate(s.DownBps, unit);
        UpValue.Text = uv; UpUnit.Text = uu;
        DownValue.Text = dv; DownUnit.Text = du;
        ContentGrid.Opacity = s.Connected ? 1.0 : 0.55;
    }

    // ───────────── placement ─────────────
    private void Reposition()
    {
        if (_hwnd == IntPtr.Zero) return;

        var tb = NativeMethods.GetTaskbar();
        _taskbar = tb;

        // Stay out of the way: fullscreen apps, auto-hidden taskbar, Explorer restarting, vertical taskbar.
        if (tb is null || tb.IsVertical || tb.IsAutoHiddenAway || NativeMethods.IsFullscreenAppActive())
        {
            NativeMethods.HideNoActivate(_hwnd);
            return;
        }

        double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int h = tb.Bounds.Height;
        int w = (int)Math.Round(WidgetWidth * s);
        if (Math.Abs(Height - h / s) > 0.5) Height = h / s;

        // Default slot: immediately left of the notification area; fallback if it can't be located.
        int anchor = tb.Tray is { } t && t.Left > tb.Bounds.Left + tb.Bounds.Width / 2
            ? t.Left
            : tb.Bounds.Right - (int)Math.Round(300 * s);

        int x = anchor - w - (int)Math.Round(4 * s) + (int)Math.Round(_settings.Current.OffsetDip * s);
        x = Math.Clamp(x, tb.Bounds.Left, tb.Bounds.Right - w);

        NativeMethods.PlaceTopmost(_hwnd, x, tb.Bounds.Top, w, h);
    }

    // ───────────── interaction: click = flyout, drag (when unlocked) = nudge horizontally ─────────────
    private int ScreenX(MouseEventArgs e) => (int)PointToScreen(e.GetPosition(this)).X;

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _dragging = false;
        _pressScreenX = ScreenX(e);
        _pressOffset = _settings.Current.OffsetDip;
        Root.CaptureMouse();
    }

    private void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _settings.Current.LockPosition) return;

        int dx = ScreenX(e) - _pressScreenX;
        if (!_dragging && Math.Abs(dx) < DragThresholdPx) return;

        _dragging = true;
        double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        _settings.Current.OffsetDip = _pressOffset + dx / s;
        Reposition();
    }

    private void Root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Root.ReleaseMouseCapture();
        bool wasDrag = _dragging;
        _pressed = _dragging = false;

        if (wasDrag) _settings.Save();
        else OpenFlyout(showSettings: false);
    }

    private void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e) => OpenFlyout(showSettings: true);

    private void OpenFlyout(bool showSettings)
    {
        var tb = _taskbar ?? NativeMethods.GetTaskbar();
        if (tb is not null) _flyout.Toggle(tb, showSettings);
    }
}
