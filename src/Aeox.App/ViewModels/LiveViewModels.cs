using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Aeox.Core.Checkup;
using Aeox.Core.Game;
using Aeox.Core.Hardware;
using Aeox.Core.Live;

namespace Aeox.App.ViewModels;

public sealed class LiveRow
{
    public LiveRow(LiveFinding finding, ICommand? fix, string? fixText)
    {
        Finding = finding;
        FixCommand = fix;
        FixText = fixText ?? string.Empty;
    }

    public LiveFinding Finding { get; }
    public string Title => Finding.Title;
    public string Detail => Finding.Detail;
    public ICommand? FixCommand { get; }
    public string FixText { get; }
    public bool HasFix => FixCommand is not null && Finding.Status == CheckStatus.Warn;
    public string Tag => Finding.Status switch { CheckStatus.Good => "[ok]", CheckStatus.Warn => "[fix]", _ => "[i]" };

    public Brush DotBrush => (Brush)System.Windows.Application.Current.Resources[Finding.Status switch
    {
        CheckStatus.Good => "SuccessBrush",
        CheckStatus.Warn => "WarnBrush",
        _ => "FaintBrush"
    }];
}

public sealed class LiveViewModel : Observable
{
    private readonly Func<IReadOnlyList<GameProfile>> _games;
    private readonly HardwareInfo _hardware;
    private readonly Action<string> _navigate;
    private bool _running;
    private string _summary = "Start a game, then hit check. Aeox also checks by itself a minute after a game starts.";
    private LiveReport? _last;

    public LiveViewModel(Func<IReadOnlyList<GameProfile>> games, HardwareInfo hardware, Action<string> navigate)
    {
        _games = games;
        _hardware = hardware;
        _navigate = navigate;
        CheckCommand = new RelayCommand(() => _ = CheckAsync(), () => !_running);
    }

    public ObservableCollection<LiveRow> Rows { get; } = new();
    public ICommand CheckCommand { get; }
    public string ButtonText => _running ? "checking..." : "check now";

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public async Task<LiveReport?> CheckAsync()
    {
        if (_running) return null;
        var game = GameMonitor.FindRunning(_games());
        if (game is null)
        {
            Summary = "No supported game is running right now. Start one and check again.";
            Rows.Clear();
            return null;
        }
        _running = true;
        Raise(nameof(ButtonText));
        CommandManager.InvalidateRequerySuggested();
        Summary = $"Measuring {game.Game.ShortName} for a few seconds...";
        try
        {
            var report = await GameMonitor.CheckAsync(game, _hardware);
            _last = report;
            Rows.Clear();
            foreach (var f in report.Findings)
            {
                ICommand? fix = f.Action switch
                {
                    LiveAction.PinCacheCores => new RelayCommand(Pin),
                    LiveAction.OpenPage when f.Page is not null => new RelayCommand(() => _navigate(f.Page)),
                    _ => null
                };
                var text = f.Action switch { LiveAction.PinCacheCores => "pin to v-cache cores", LiveAction.OpenPage => "open windows", _ => null };
                Rows.Add(new LiveRow(f, fix, text));
            }
            var warns = report.Findings.Count(f => f.Status == CheckStatus.Warn);
            Summary = warns == 0 ? $"{report.GameName} runs the way it should." : $"{report.GameName}: {warns} thing{(warns == 1 ? "" : "s")} to fix.";
            return report;
        }
        finally
        {
            _running = false;
            Raise(nameof(ButtonText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Pin()
    {
        if (_last is null) return;
        Summary = GameMonitor.PinToCacheCores(_last.ProcessId, _hardware.Threads)
            ? $"{_last.GameName} is pinned to the V-Cache cores until it closes. For a permanent fix, turn on Games on V-Cache cores in windows."
            : $"Windows or the game's anti-cheat blocked pinning {_last.GameName}. Use Games on V-Cache cores in windows or the BIOS option instead.";
    }
}

public sealed class BackgroundRow : Observable
{
    private readonly Action _changed;
    private bool _isSelected;

    public BackgroundRow(RunningApp app, bool selected, Action changed)
    {
        App = app;
        _isSelected = selected;
        _changed = changed;
    }

    public RunningApp App { get; }
    public string Name => App.Name;

    public string Detail
    {
        get
        {
            var parts = new List<string> { $"{App.MemoryBytes / 1024.0 / 1024:0} MB", App.HasWindow ? "open window" : "background" };
            if (!string.IsNullOrWhiteSpace(App.Company)) parts.Add(App.Company!);
            if (App.Hint is not null) parts.Add(App.Hint);
            return string.Join("  ·  ", parts);
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value)) _changed();
        }
    }
}

public sealed class BackgroundViewModel : Observable
{
    private readonly AppSettings _settings;
    private string _summary = string.Empty;
    private bool _busy;

    public BackgroundViewModel(AppSettings settings)
    {
        _settings = settings;
        RefreshCommand = new RelayCommand(Refresh);
        CloseCommand = new RelayCommand(() => _ = CloseSelectedAsync(), () => !_busy && Rows.Any(r => r.IsSelected));
    }

    public ObservableCollection<BackgroundRow> Rows { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand CloseCommand { get; }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public bool AutoClose
    {
        get => _settings.CloseAppsOnGameStart;
        set
        {
            _settings.CloseAppsOnGameStart = value;
            _settings.Save();
            Raise();
        }
    }

    public void Refresh()
    {
        var running = BackgroundApps.Scan(BackgroundApps.GameProcessNames());
        var chosen = Chosen(_settings).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Rows.Clear();
        foreach (var app in running) Rows.Add(new BackgroundRow(app, chosen.Contains(app.Key), Remember));
        var background = running.Where(r => !r.HasWindow).ToList();
        var mb = running.Sum(r => r.MemoryBytes) / 1024.0 / 1024;
        Summary = running.Count == 0
            ? "Nothing to close right now."
            : $"{running.Count} programs you could close ({background.Count} in the background), using {mb:0} MB of RAM. Pick what you don't need.";
        CommandManager.InvalidateRequerySuggested();
    }

    public async Task<int> CloseSelectedAsync()
    {
        var keys = Rows.Where(r => r.IsSelected).Select(r => r.App.Key).ToList();
        if (keys.Count == 0) return 0;
        _busy = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var closed = await BackgroundApps.CloseAsync(keys, BackgroundApps.GameProcessNames());
            Refresh();
            Summary = $"Closed {keys.Count} program{(keys.Count == 1 ? "" : "s")}. " + Summary;
            return closed;
        }
        finally
        {
            _busy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public static IEnumerable<string> Chosen(AppSettings settings) => settings.CloseApps ?? new List<string>();

    private void Remember()
    {
        var chosen = Chosen(_settings).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Rows)
        {
            if (r.IsSelected) chosen.Add(r.App.Key);
            else chosen.Remove(r.App.Key);
        }
        _settings.CloseApps = chosen.ToList();
        _settings.Save();
        CommandManager.InvalidateRequerySuggested();
    }
}
