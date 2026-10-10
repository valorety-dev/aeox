using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Aeox.Core.Aim;
using Aeox.Core.Tweaks;
using Microsoft.Web.WebView2.Core;

namespace Aeox.Aim;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static string ProfilePath => Path.Combine(AeoxContext.DefaultDataDir(), "aim", "profile.json");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await StartAsync();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            var square = 1;
            DwmSetWindowAttribute(hwnd, 33, ref square, sizeof(int));
        };
        StateChanged += (_, _) => Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
    }

    private async Task StartAsync()
    {
        try
        {
            var userData = Path.Combine(AeoxContext.DefaultDataDir(), "webview-aim");
            var options = new CoreWebView2EnvironmentOptions("--disable-gpu-vsync --disable-frame-rate-limit");
            var env = await CoreWebView2Environment.CreateAsync(null, userData, options);
            await View.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            StatusText.Text = "microsoft edge webview2 is missing. install it from microsoft and reopen aeox aim.";
            return;
        }
        var core = View.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.SetVirtualHostNameToFolderMapping("aim.aeox", Path.Combine(AppContext.BaseDirectory, "web"), CoreWebView2HostResourceAccessKind.DenyCors);
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
        var type = doc.RootElement.GetProperty("type").GetString();
        switch (type)
        {
            case "ready":
                var profile = File.Exists(ProfilePath) ? File.ReadAllText(ProfilePath) : "null";
                var imports = await Task.Run(AimImport.FindAll);
                View.CoreWebView2.PostWebMessageAsJson(
                    $"{{\"type\":\"init\",\"profile\":{profile},\"imports\":{JsonSerializer.Serialize(imports, Json)}}}");
                break;
            case "save":
                var data = doc.RootElement.GetProperty("data").GetRawText();
                Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
                var tmp = ProfilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, data);
                File.Move(tmp, ProfilePath, true);
                break;
            case "drag":
                var hwnd = new WindowInteropHelper(this).Handle;
                ReleaseCapture();
                SendMessage(hwnd, 0xA1, (IntPtr)2, IntPtr.Zero);
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
