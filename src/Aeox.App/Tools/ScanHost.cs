using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Aeox.Core.Hardware;
using Aeox.Core.Tweaks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Aeox.App.Tools;

public sealed class ScanHost
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly WebView2 _view;
    private readonly Action<string> _status;
    private bool _started;
    private bool _ready;

    public ScanHost(WebView2 view, Action<string> status)
    {
        _view = view;
        _status = status;
    }

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AeoxContext.DefaultDataDir(), "webview"));
            await _view.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _status("microsoft edge webview2 is missing. install it from microsoft and reopen aeox.");
            return;
        }
        var core = _view.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.SetVirtualHostNameToFolderMapping("scan.aeox", Path.Combine(AppContext.BaseDirectory, "web", "scan"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.WebMessageReceived += OnMessage;
        core.NavigationStarting += (_, e) =>
        {
            if (!e.Uri.StartsWith("https://scan.aeox/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
        };
        core.Navigate("https://scan.aeox/index.html");
    }

    public async Task RescanAsync()
    {
        if (!_ready) return;
        _status("scanning...");
        var result = await Task.Run(() => SystemScan.Run(AeoxContext.DefaultDataDir()));
        _view.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "scan", data = result }, Json));
        _status($"scanned {DateTime.Now:HH:mm}.");
    }

    public void SaveCard()
    {
        if (_ready) _view.CoreWebView2.PostWebMessageAsJson("{\"type\":\"card\"}");
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var doc = JsonDocument.Parse(e.WebMessageAsJson);
        var type = doc.RootElement.GetProperty("type").GetString();
        if (type == "ready")
        {
            _ready = true;
            await RescanAsync();
        }
        else if (type == "card")
        {
            var dataUrl = doc.RootElement.GetProperty("data").GetString();
            if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal)) return;
            var bytes = Convert.FromBase64String(dataUrl["data:image/png;base64,".Length..]);
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Aeox");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"aeox-setup-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            await File.WriteAllBytesAsync(file, bytes);
            _status("card saved to pictures\\aeox.");
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        }
    }
}
