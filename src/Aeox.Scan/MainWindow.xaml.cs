using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Aeox.Core.Hardware;
using Aeox.Core.Tweaks;
using Microsoft.Web.WebView2.Core;

namespace Aeox.Scan;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private bool _pageReady;

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
        StateChanged += (_, _) =>
        {
            Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        };
    }

    private async Task StartAsync()
    {
        try
        {
            var userData = Path.Combine(AeoxContext.DefaultDataDir(), "webview");
            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await View.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            StatusText.Text = "microsoft edge webview2 is missing. install it from microsoft and reopen aeox scan.";
            return;
        }
        var core = View.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.SetVirtualHostNameToFolderMapping("scan.aeox", Path.Combine(AppContext.BaseDirectory, "web"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.WebMessageReceived += OnMessage;
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith("https://scan.aeox/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
        };
        core.Navigate("https://scan.aeox/index.html");
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        var type = doc.RootElement.GetProperty("type").GetString();
        if (type == "ready")
        {
            _pageReady = true;
            await ScanAsync();
        }
        else if (type == "card")
        {
            SaveCard(doc.RootElement.GetProperty("data").GetString());
        }
    }

    private async Task ScanAsync()
    {
        if (!_pageReady) return;
        RescanButton.IsEnabled = false;
        StatusText.Text = "scanning…";
        var result = await Task.Run(() => SystemScan.Run(AeoxContext.DefaultDataDir()));
        var payload = JsonSerializer.Serialize(new { type = "scan", data = result }, Json);
        View.CoreWebView2.PostWebMessageAsJson(payload);
        StatusText.Text = $"scanned {DateTime.Now:HH:mm}.";
        RescanButton.IsEnabled = true;
    }

    private void SaveCard(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal)) return;
        var bytes = Convert.FromBase64String(dataUrl["data:image/png;base64,".Length..]);
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Aeox");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"aeox-setup-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        File.WriteAllBytes(file, bytes);
        StatusText.Text = $"card saved to pictures\\aeox.";
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (!_pageReady) return;
        View.CoreWebView2.PostWebMessageAsJson("{\"type\":\"card\"}");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
