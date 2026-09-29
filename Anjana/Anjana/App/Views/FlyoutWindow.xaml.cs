using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Anjana.Interop;
using Anjana.Services;

namespace Anjana.Views;

public partial class FlyoutWindow : Window
{
    private readonly SettingsService _settings;
    private readonly NetworkMonitor _monitor;
    private NativeMethods.TaskbarInfo? _taskbar;
    private IntPtr _hwnd;
    private DateTime _lastHiddenUtc = DateTime.MinValue;
    private bool _loading;

    public FlyoutWindow(SettingsService settings, NetworkMonitor monitor)
    {
        InitializeComponent();
        _settings = settings;
        _monitor = monitor;

        Root.RenderTransform = new TranslateTransform();
        _monitor.Updated += s => { if (IsVisible) Render(s); };
        _settings.Changed += () => { if (IsVisible) Render(_monitor.Current); };
        Deactivated += (_, _) => HideFlyout();
        SizeChanged += (_, _) => PositionWindow();
        ThemeService.Changed += () => { if (_hwnd != IntPtr.Zero) NativeMethods.ApplyFlyoutChrome(_hwnd, !ThemeService.IsLight); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ApplyFlyoutChrome(_hwnd, !ThemeService.IsLight);

        if (NativeMethods.SupportsBackdrop)
        {
            // Let DWM's acrylic show through; DWM draws the rounded border itself.
            HwndSource.FromHwnd(_hwnd)!.CompositionTarget.BackgroundColor = Colors.Transparent;
            Root.BorderThickness = new Thickness(0);
        }
        else
        {
            // Win10 / early Win11: solid surface fallback.
            Root.SetResourceReference(Border.BackgroundProperty, "SurfaceSolid");
        }
    }

    // ───────────── show / hide ─────────────
    public void Toggle(NativeMethods.TaskbarInfo taskbar, bool showSettings)
    {
        if (IsVisible) { HideFlyout(); return; }
        if ((DateTime.UtcNow - _lastHiddenUtc).TotalMilliseconds < 250) return; // the click that just dismissed us

        _taskbar = taskbar;
        SettingsPanel.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
        LoadSettingsState();
        Render(_monitor.Current);

        Root.Opacity = 0;
        Show();
        PositionWindow();
        Activate();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        ((TranslateTransform)Root.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
    }

    private void HideFlyout()
    {
        if (!IsVisible) return;
        Hide();
        _lastHiddenUtc = DateTime.UtcNow;
    }

    /// <summary>Win11 flyout geometry: 12 px above the taskbar, 12 px from the screen's right edge.</summary>
    private void PositionWindow()
    {
        if (_taskbar is null || _hwnd == IntPtr.Zero || !IsVisible) return;
        UpdateLayout();
        double s = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int w = (int)Math.Round(ActualWidth * s);
        int h = (int)Math.Round(ActualHeight * s);
        int gap = (int)Math.Round(12 * s);
        NativeMethods.Move(_hwnd, _taskbar.Monitor.Right - gap - w, _taskbar.Bounds.Top - gap - h, w, h);
    }

    // ───────────── content ─────────────
    private void Render(NetSnapshot s)
    {
        var unit = _settings.Current.Unit;
        var (dv, du) = SpeedFormatter.Rate(s.DownBps, unit);
        var (uv, uu) = SpeedFormatter.Rate(s.UpBps, unit);

        DownBig.Text = dv; DownBigUnit.Text = du;
        UpBig.Text = uv; UpBigUnit.Text = uu;
        DownGraph.Values = _monitor.DownHistory;
        UpGraph.Values = _monitor.UpHistory;
        TotalDown.Text = SpeedFormatter.Size(s.TotalDown);
        TotalUp.Text = SpeedFormatter.Size(s.TotalUp);

        AdapterName.Text = s.Adapter;
        StatusText.Text = s.Connected ? "Connected" : "No connection";
        AdapterIcon.Text = s.IsWireless ? "\uE701" : "\uE839";
    }

    private void LoadSettingsState()
    {
        _loading = true;
        UnitBytes.IsChecked = _settings.Current.Unit == SpeedUnit.Bytes;
        UnitBits.IsChecked = _settings.Current.Unit == SpeedUnit.Bits;
        LockToggle.IsChecked = _settings.Current.LockPosition;
        StartupToggle.IsChecked = StartupService.IsEnabled;
        _loading = false;
    }

    // ───────────── settings handlers ─────────────
    private void Gear_Click(object sender, RoutedEventArgs e) =>
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void Unit_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton rb) return;
        var unit = Enum.Parse<SpeedUnit>((string)rb.Tag);
        _settings.Update(s => s.Unit = unit);
    }

    private void Lock_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Update(s => s.LockPosition = LockToggle.IsChecked == true);
    }

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        StartupService.Set(StartupToggle.IsChecked == true);
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
