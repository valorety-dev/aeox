using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aeox.App.ViewModels;

namespace Aeox.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelChanged;
        ApplyStatusVisual();
        SourceInitialized += (_, _) => ApplyWindowFrame();
        StateChanged += (_, _) => OnStateChanged();
        Activated += (_, _) => _vm.Refresh();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Status)) ApplyStatusVisual();
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

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyWindowFrame()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        var round = 2;
        DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
