using System.IO;
using System.Windows;
using System.Windows.Threading;
using Aeox.App.ViewModels;
using Aeox.Core.Game;
using Aeox.Core.Stats;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Aeox.App;

public sealed class AutoMode : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly Window _window;
    private readonly MainViewModel _vm;
    private readonly Forms.NotifyIcon _tray;
    private readonly DispatcherTimer _timer;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private bool _gameWasRunning;
    private DateTime _sessionStartUtc;
    private DateTime _lastDriftCheck = DateTime.MinValue;
    private bool _trayHintShown;
    private bool _noted;
    private bool _anyWasRunning;
    private bool _liveChecked;
    private DateTime _anyStartUtc;

    public AutoMode(Window window, MainViewModel vm)
    {
        _window = window;
        _vm = vm;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Aeox", null, (_, _) => ShowWindow());
        _pauseItem = new Forms.ToolStripMenuItem("Pause auto mode") { CheckOnClick = true };
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/aeox.ico"))!.Stream;
        _tray = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream),
            Text = "Aeox",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowWindow();

        _gameWasRunning = GameRunning.IsGameRunning();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public event Action? ExitRequested;

    public static void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled && Environment.ProcessPath is { } exe) key.SetValue("Aeox", $"\"{exe}\" --tray");
        else key.DeleteValue("Aeox", false);
    }

    public void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void HintRunningInTray()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        Notify("Aeox is still running", "Auto mode keeps your settings in place. Right-click the tray icon to exit.");
    }

    public void Notify(string title, string text) => _tray.ShowBalloonTip(6000, title, text, Forms.ToolTipIcon.None);

    private void Tick()
    {
        var running = GameRunning.IsGameRunning();
        if (running && !_gameWasRunning) _sessionStartUtc = DateTime.UtcNow;
        if (running && (DateTime.UtcNow - _sessionStartUtc).TotalSeconds > 20 && !_noted)
        {
            _noted = true;
            _vm.NoteRunningGame();
        }
        if (!running) _noted = false;
        WatchAnyGame();
        if (!running && _gameWasRunning) _ = OnGameClosedAsync(_sessionStartUtc);
        _gameWasRunning = running;

        if (running || _pauseItem.Checked || !_vm.Settings.AutoReapply) return;
        if ((DateTime.UtcNow - _lastDriftCheck).TotalSeconds < 30) return;
        _lastDriftCheck = DateTime.UtcNow;
        var fixedCount = _vm.ReapplyRemembered();
        if (fixedCount > 0)
            Notify("Settings restored", $"Something reset {fixedCount} of your settings. Aeox put them back.");
    }

    private void WatchAnyGame()
    {
        var game = Aeox.Core.Live.GameMonitor.FindRunning(_vm.AllGames);
        var any = game is not null;
        if (any && !_anyWasRunning)
        {
            _anyStartUtc = DateTime.UtcNow;
            _liveChecked = false;
            if (_vm.Settings.CloseAppsOnGameStart && !_pauseItem.Checked) _ = CloseAppsAsync(game!.Game.ShortName);
        }
        if (any && !_liveChecked && !_pauseItem.Checked && (DateTime.UtcNow - _anyStartUtc).TotalSeconds > 60)
        {
            _liveChecked = true;
            _ = LiveCheckAsync();
        }
        _anyWasRunning = any;
    }

    private async Task CloseAppsAsync(string gameName)
    {
        var closed = await Aeox.Core.Live.BackgroundApps.CloseAsync(BackgroundViewModel.Chosen(_vm.Settings), Aeox.Core.Live.BackgroundApps.GameProcessNames());
        if (closed > 0) Notify("Background apps closed", $"Closed {closed} background process{(closed == 1 ? "" : "es")} for {gameName}.");
    }

    private async Task LiveCheckAsync()
    {
        var report = await _vm.Live.CheckAsync();
        var warn = report?.Findings.FirstOrDefault(f => f.Status == Aeox.Core.Checkup.CheckStatus.Warn);
        if (report is not null && warn is not null) Notify($"{report.GameName}: {warn.Title}", warn.Detail.Length > 200 ? warn.Detail[..200] + "..." : warn.Detail);
    }

    private async Task OnGameClosedAsync(DateTime sessionStartUtc)
    {
        if (!_vm.Settings.SessionReports) return;
        await Task.Delay(TimeSpan.FromSeconds(4));
        var saved = GameCatalog.SavedDirs()
            .Where(d => File.Exists(new GamePaths(d).GameLog))
            .OrderByDescending(d => File.GetLastWriteTimeUtc(new GamePaths(d).GameLog))
            .FirstOrDefault();
        if (saved is null) return;
        var paths = new GamePaths(saved);
        var name = _vm.NameForSavedDir(saved);

        var (matches, crashed) = await Task.Run(() =>
        {
            var history = MatchHistory.Load(Aeox.Core.Tweaks.AeoxContext.DefaultDataDir(), GameCatalog.HistoryKey(saved));
            history.ImportLogs(paths.LogDir);
            var session = history.Data.Matches.Where(m => m.TimeUtc >= sessionStartUtc.AddMinutes(-1)).ToList();
            return (session, !EndedCleanly(paths.GameLog));
        });

        if (crashed)
        {
            Notify($"{name} closed unexpectedly", "The game did not exit normally. If it keeps happening, open Aeox Checkup.");
            return;
        }
        if (matches.Count == 0) return;
        var fps = matches.Average(m => m.AvgFps);
        var pings = matches.Where(m => m.PingMs is not null).Select(m => m.PingMs!.Value).ToList();
        var ping = pings.Count > 0 ? $", ping {pings.Average():0} ms" : string.Empty;
        Notify($"{name} session", $"{matches.Count} match{(matches.Count == 1 ? "" : "es")}, {fps:0} FPS average{ping}.");
    }

    private static bool EndedCleanly(string log)
    {
        try
        {
            using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(-Math.Min(fs.Length, 8192), SeekOrigin.End);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd().Contains("LogExit: Exiting.", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return true;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
    }
}
