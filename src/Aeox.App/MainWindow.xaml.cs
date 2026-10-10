using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aeox.App.Tools;
using Aeox.App.ViewModels;

namespace Aeox.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ScanHost _scan;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _scan = new ScanHost(ScanView, text => ScanStatus.Text = text);
        _vm.PropertyChanged += OnViewModelChanged;
        ApplyStatusVisual();
        SourceInitialized += (_, _) => ApplyWindowFrame();
        StateChanged += (_, _) => OnStateChanged();
        Activated += (_, _) => _vm.Refresh();

        _auto = new AutoMode(this, _vm);
        _auto.ExitRequested += ExitApp;
        if (_vm.Settings.StartWithWindows) AutoMode.SetStartWithWindows(true);
        Closing += OnClosing;
    }

    private readonly AutoMode _auto;
    private bool _exiting;

    public void ShowFromTray() => _auto.ShowWindow();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting || !_vm.Settings.CloseToTray)
        {
            _auto.Dispose();
            Application.Current.Shutdown();
            return;
        }
        e.Cancel = true;
        Hide();
        _auto.HintRunningInTray();
    }

    private void ExitApp()
    {
        _exiting = true;
        Close();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Status)) ApplyStatusVisual();
        if (e.PropertyName == nameof(MainViewModel.ShowPreview))
        {
            PreviewColumn.Width = new GridLength(_vm.ShowPreview ? 400 : 0);
            PreviewPanel.Visibility = _vm.ShowPreview ? Visibility.Visible : Visibility.Collapsed;
        }
        if (e.PropertyName == nameof(MainViewModel.IsScanPage) && _vm.IsScanPage) _ = _scan.StartAsync();
    }

    private void ApplyStatusVisual()
    {
        var (dot, glyph, title) = _vm.Status switch
        {
            StatusKind.Pending => ("AccentBrush", "\uE895", "TextBrush"),
            StatusKind.Warning => ("WarnBrush", "\uE7BA", "WarnBrush"),
            StatusKind.Error => ("DangerBrush", "\uE711", "DangerBrush"),
            _ => ("SuccessBrush", "\uE73E", "TextBrush")
        };
        StatusDot.Fill = (Brush)FindResource(dot);
        StatusGlyph.Text = glyph;
        StatusTitleText.Foreground = (Brush)FindResource(title);
    }

    private void OnStateChanged()
    {
        Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaxButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
    }

    private void DriverTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton { Tag: string page }) _vm.Driver.Page = page;
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await _scan.RescanAsync();

    private void ScanCard_Click(object sender, RoutedEventArgs e) => _scan.SaveCard();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyWindowFrame()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        var round = 1;
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
