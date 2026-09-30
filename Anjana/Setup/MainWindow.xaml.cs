using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Anjana.Setup;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static readonly Brush NoteNormal = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    private static readonly Brush NoteError = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));

    private string _installDir = "";
    private bool _succeeded;

    public MainWindow()
    {
        InitializeComponent();
        PathBox.Text = AppInfo.DefaultInstallDir;

        void drag(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
        BrandPanel.MouseLeftButtonDown += drag;
        DragStrip.MouseLeftButtonDown += drag;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        int round = 2; // DWMWCP_ROUND — Windows 11 rounded corners
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref round, 4);
    }

    private void Show(UIElement page)
    {
        PageOptions.Visibility = PageProgress.Visibility = PageDone.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose where to install Anjana" };
        if (dlg.ShowDialog(this) != true) return;

        // Always install into a dedicated "Anjana" folder so nothing else in the chosen folder is touched.
        string picked = dlg.FolderName.TrimEnd('\\');
        PathBox.Text = string.Equals(Path.GetFileName(picked), AppInfo.Name, StringComparison.OrdinalIgnoreCase)
            ? picked
            : Path.Combine(picked, AppInfo.Name);
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        string dir;
        try
        {
            dir = Path.GetFullPath(PathBox.Text.Trim());
            if (string.Equals(Path.GetPathRoot(dir), dir, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Drive root not allowed");
        }
        catch
        {
            Note.Text = "Please choose a valid folder, for example a new folder named Anjana.";
            Note.Foreground = NoteError;
            return;
        }

        _installDir = dir;
        Show(PageProgress);

        var progress = new Progress<(double Percent, string Status)>(t =>
        {
            Bar.Value = t.Percent;
            StatusText.Text = t.Status;
            PercentText.Text = $"{(int)t.Percent}%";
        });
        var options = new InstallOptions(dir, StartupCheck.IsChecked == true,
                                         StartMenuCheck.IsChecked == true, DesktopCheck.IsChecked == true);
        try
        {
            await Task.Run(() => Installer.Run(options, progress));
            await Task.Delay(350);
            _succeeded = true;
            DoneTitle.Text = $"{AppInfo.Name} is ready";
            DoneText.Text = "Look for the speed readout beside your system tray.\nClick it for details; right-click for settings.";
        }
        catch (Exception ex)
        {
            _succeeded = false;
            DoneBadge.Background = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
            DoneGlyph.Text = "\uE711"; // cross
            DoneTitle.Text = "Installation failed";
            DoneText.Text = ex.Message;
            LaunchCheck.Visibility = Visibility.Collapsed;
            FinishButton.Content = "Close";
        }
        Show(PageDone);
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (_succeeded && LaunchCheck.IsChecked == true)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_installDir, AppInfo.ExeName))
                {
                    UseShellExecute = true,
                    WorkingDirectory = _installDir
                });
            }
            catch { /* user can start it from the Start menu */ }
        }
        Close();
    }
}
