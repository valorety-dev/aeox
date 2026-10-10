using System.Collections.ObjectModel;
using Aeox.Core.Stats;
using Aeox.Core.Tweaks;

namespace Aeox.App.ViewModels;

public sealed record MatchRow(string When, string Fps, string Stutter, string Ping, string Limit);

public sealed class StatsViewModel : Observable
{
    private readonly Func<AeoxContext> _ctx;
    private MatchHistory? _history;
    private string _headline = "Loading your matches...";
    private string _compare = string.Empty;
    private string _bottleneck = string.Empty;
    private IReadOnlyList<double> _values = Array.Empty<double>();
    private IReadOnlyList<int> _marks = Array.Empty<int>();

    public StatsViewModel(Func<AeoxContext> ctx)
    {
        _ctx = ctx;
    }

    public ObservableCollection<MatchRow> Recent { get; } = new();

    public string Headline
    {
        get => _headline;
        private set => Set(ref _headline, value);
    }

    public string Compare
    {
        get => _compare;
        private set => Set(ref _compare, value);
    }

    public string Bottleneck
    {
        get => _bottleneck;
        private set => Set(ref _bottleneck, value);
    }

    public IReadOnlyList<double> Values
    {
        get => _values;
        private set => Set(ref _values, value);
    }

    public IReadOnlyList<int> Marks
    {
        get => _marks;
        private set => Set(ref _marks, value);
    }

    public async Task LoadAsync()
    {
        var ctx = _ctx();
        var history = await Task.Run(() =>
        {
            var h = MatchHistory.Load(ctx.DataDir, ctx.Game.HistoryKey);
            h.ImportLogs(ctx.Paths.LogDir);
            return h;
        });
        _history = history;
        Present(ctx);
    }

    public void RecordApply(string summary)
    {
        var ctx = _ctx();
        _history ??= MatchHistory.Load(ctx.DataDir, ctx.Game.HistoryKey);
        _history.AddApply(summary);
    }

    private void Present(AeoxContext ctx)
    {
        var matches = _history!.Data.Matches;
        var shown = matches.TakeLast(40).ToList();
        Values = shown.Select(m => m.AvgFps).ToList();
        Marks = _history.Data.Applies
            .Select(a => shown.FindIndex(m => m.TimeUtc > a.TimeUtc))
            .Where(i => i > 0)
            .Distinct()
            .ToList();

        if (matches.Count == 0)
        {
            Headline = $"No {ctx.Game.ShortName} matches recorded yet.";
            Compare = "Play a match, then come back. Aeox reads the stats Fortnite writes after every game.";
            Bottleneck = string.Empty;
            Recent.Clear();
            return;
        }

        var last10 = matches.TakeLast(10).ToList();
        Headline = $"{last10.Average(m => m.AvgFps):0} FPS average over your last {last10.Count} match{(last10.Count == 1 ? "" : "es")}";

        var cmp = _history.CompareAroundLastApply();
        Compare = cmp is { } c
            ? $"Before your last change {c.Before:0} FPS -> after {c.After:0} FPS  ({(c.After - c.Before) / c.Before * 100:+0;-0}%, {c.AfterCount} match{(c.AfterCount == 1 ? "" : "es")} since)"
            : _history.Data.Applies.Count > 0
                ? "Play a match to see the effect of your last change."
                : "Apply a change and play a few matches to see before and after here.";

        var latest = matches[^1];
        var capText = Aeox.Core.Ini.IniDocument.Load(ctx.Paths.GameUserSettings).Get(Aeox.Core.Game.RetracGame.UserSettingsSection, "FrameRateLimit");
        var cap = double.TryParse(capText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c2) ? c2 : 0;
        Bottleneck = cap > 0 && latest.AvgFps >= cap * 0.9
            ? $"Your matches sit near the in-game {cap:0} FPS cap, so the cap is what limits you. Turn on Uncapped frame rate for more."
            : latest.Bottleneck switch
        {
            "CPU" => $"Last match was CPU-limited (game {latest.GameThreadMs:0.0} ms, render {latest.RenderThreadMs:0.0} ms, GPU {latest.GpuMs:0.0} ms). Lower graphics settings will not help much; background apps and CPU settings will.",
            "GPU" => $"Last match was GPU-limited (GPU {latest.GpuMs:0.0} ms per frame). Lower render scale or resolution for more FPS.",
            "balanced" => "Last match had CPU and GPU evenly loaded.",
            _ => string.Empty
        };

        Recent.Clear();
        foreach (var m in matches.TakeLast(12).Reverse())
        {
            Recent.Add(new MatchRow(
                m.TimeUtc.ToLocalTime().ToString("ddd dd MMM  HH:mm"),
                $"{m.AvgFps:0} fps",
                $"{m.HitchesPerMin:0.0} hitches/min",
                m.PingMs is null ? "-" : $"{m.PingMs:0} ms",
                m.Bottleneck switch { "CPU" => "cpu", "GPU" => "gpu", "balanced" => "even", _ => "" }));
        }
    }
}
