using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Aeox.Core.Aim;
using Aeox.Core.Tweaks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Aeox.App.Tools;

public sealed class AimWindow : Window
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly WebView2 _view = new() { DefaultBackgroundColor = System.Drawing.Color.FromArgb(11, 11, 12) };
    private readonly Grid _root = new();

    public static string ProfilePath => Path.Combine(AeoxContext.DefaultDataDir(), "aim", "profile.json");

    public AimWindow()
    {
        Title = "Aeox Aim";
        Width = 1600;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Background = new SolidColorBrush(Color.FromRgb(11, 11, 12));
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/aeox.ico"));
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        _root.Children.Add(_view);
        Content = _root;
        Loaded += async (_, _) => await StartAsync();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            var square = 1;
            DwmSetWindowAttribute(hwnd, 33, ref square, sizeof(int));
        };
        StateChanged += (_, _) => _root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
    }

    private async Task StartAsync()
    {
        try
        {
            var options = new CoreWebView2EnvironmentOptions("--disable-gpu-vsync --disable-frame-rate-limit");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AeoxContext.DefaultDataDir(), "webview-aim"), options);
            await _view.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show("Microsoft Edge WebView2 is missing. Install it from Microsoft and reopen the aim trainer.", "Aeox Aim");
            Close();
            return;
        }
        var core = _view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.SetVirtualHostNameToFolderMapping("aim.aeox", Path.Combine(AppContext.BaseDirectory, "web", "aim"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.WebMessageReceived += OnMessage;
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith("https://aim.aeox/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
        };
        core.Navigate("https://aim.aeox/index.html");
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        switch (doc.RootElement.GetProperty("type").GetString())
        {
            case "ready":
                var profile = File.Exists(ProfilePath) ? await File.ReadAllTextAsync(ProfilePath) : "null";
                var imports = await Task.Run(AimImport.FindAll);
                _view.CoreWebView2.PostWebMessageAsJson($"{{\"type\":\"init\",\"profile\":{profile},\"imports\":{JsonSerializer.Serialize(imports, Json)}}}");
                break;
            case "save":
                Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
                var tmp = ProfilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, doc.RootElement.GetProperty("data").GetRawText());
                File.Move(tmp, ProfilePath, true);
                break;
            case "drag":
                ReleaseCapture();
                SendMessage(new WindowInteropHelper(this).Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                break;
            case "min":
                WindowState = WindowState.Minimized;
                break;
            case "max":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "close":
                Close();
                break;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}
