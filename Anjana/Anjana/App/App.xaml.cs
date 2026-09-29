using System.Windows;
using Anjana.Services;
using Anjana.Views;

namespace Anjana;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private NetworkMonitor? _monitor;
    private SettingsService? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--uninstall")) { Uninstaller.Run(); Shutdown(); return; }

        _mutex = new Mutex(true, @"Local\Anjana.SingleInstance", out _ownsMutex);
        if (!_ownsMutex) { Shutdown(); return; }

        ThemeService.Initialize();
        _settings = SettingsService.Load();
        _monitor = new NetworkMonitor();

        var flyout = new FlyoutWindow(_settings, _monitor);
        var widget = new TaskbarWidget(_settings, _monitor, flyout);
        MainWindow = widget;
        widget.Show();
        _monitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings?.Save();
        _monitor?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
