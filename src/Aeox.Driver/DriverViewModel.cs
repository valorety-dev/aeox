using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Windows.Input;
using Aeox.Core.Drivers;
using Aeox.Core.Game;
using Aeox.Core.Stats;
using Aeox.Core.Tweaks;

namespace Aeox.Driver;

public sealed record HistoryRow(string Version, string Matches, string Fps, string Stutter, string Crashes, string Seen, bool IsCurrent);

public sealed class ReleaseRow
{
    public ReleaseRow(DriverRelease release, string tag)
    {
        Release = release;
        Tag = tag;
        DownloadCommand = new RelayCommand(() => Open(release.DownloadUrl));
        NotesCommand = new RelayCommand(() => Open(release.DetailsUrl), () => release.DetailsUrl is not null);
    }

    public DriverRelease Release { get; }
    public string Version => Release.Version;
    public string Date => Release.Released == DateTime.MinValue ? "" : Release.Released.ToString("MMM dd yyyy", CultureInfo.InvariantCulture).ToLowerInvariant();
    public string Headline => Release.Headline.ToLowerInvariant();
    public string Tag { get; }
    public bool HasTag => Tag.Length > 0;
    public ICommand DownloadCommand { get; }
    public ICommand NotesCommand { get; }

    private static void Open(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

public sealed class DriverViewModel : Observable
{
    private string _page = "Overview";
    private string _gpuName = "detecting...";
    private string _installed = "-";
    private string _installedNote = string.Empty;
    private string _newest = "-";
    private string _newestNote = string.Empty;
    private string _verdict = "reading your game logs and nvidia's driver list...";
    private string _verdictDetail = string.Empty;
    private string _releasesNote = string.Empty;
    private ReleaseRow? _primaryAction;

    public DriverViewModel()
    {
        RefreshCommand = new RelayCommand(() => _ = LoadAsync());
        PrimaryCommand = new RelayCommand(() => _primaryAction?.DownloadCommand.Execute(null), () => _primaryAction is not null);
    }

    public ObservableCollection<HistoryRow> History { get; } = new();
    public ObservableCollection<ReleaseRow> Releases { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand PrimaryCommand { get; }

    public string Page
    {
        get => _page;
        set
        {
            if (!Set(ref _page, value)) return;
            Raise(nameof(IsOverview));
            Raise(nameof(IsHistory));
            Raise(nameof(IsReleases));
        }
    }

    public bool IsOverview => Page == "Overview";
    public bool IsHistory => Page == "History";
    public bool IsReleases => Page == "Releases";

    public string GpuName { get => _gpuName; private set => Set(ref _gpuName, value); }
    public string Installed { get => _installed; private set => Set(ref _installed, value); }
    public string InstalledNote { get => _installedNote; private set => Set(ref _installedNote, value); }
    public string Newest { get => _newest; private set => Set(ref _newest, value); }
    public string NewestNote { get => _newestNote; private set => Set(ref _newestNote, value); }
    public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
    public string VerdictDetail { get => _verdictDetail; private set => Set(ref _verdictDetail, value); }
    public string ReleasesNote { get => _releasesNote; private set => Set(ref _releasesNote, value); }
    public string PrimaryText => _primaryAction is null ? string.Empty : $"get {_primaryAction.Version}";
    public bool HasPrimary => _primaryAction is not null;
    public bool HasHistory => History.Count > 0;

    public async Task LoadAsync()
    {
        var gpus = await Task.Run(GpuDrivers.Installed);
        var nvidia = gpus.FirstOrDefault(g => g.IsNvidia);
        var main = nvidia ?? gpus.FirstOrDefault();
        if (main is null)
        {
            GpuName = "no graphics card found";
            Verdict = "windows did not report a graphics card.";
            return;
        }

        GpuName = main.Name.ToLowerInvariant();
        Installed = main.FriendlyVersion;
        InstalledNote = main.DriverDate is { } d ? "driver date " + d.ToString("MMM dd yyyy", CultureInfo.InvariantCulture).ToLowerInvariant() : string.Empty;

        var summaries = await Task.Run(LoadSummaries);
        History.Clear();
        foreach (var s in summaries)
        {
            History.Add(new HistoryRow(
                s.Version,
                $"{s.Matches}",
                s.AvgFps is null ? "-" : $"{s.AvgFps:0}",
                s.AvgHitches is null ? "-" : $"{s.AvgHitches:0.0}",
                s.Sessions == 0 ? "-" : $"{s.Crashes}/{s.Sessions}",
                (s.FirstSeenUtc.ToLocalTime().ToString("MMM dd", CultureInfo.InvariantCulture) + " – " + s.LastSeenUtc.ToLocalTime().ToString("MMM dd", CultureInfo.InvariantCulture)).ToLowerInvariant(),
                s.Version == Installed));
        }
        Raise(nameof(HasHistory));

        IReadOnlyList<DriverRelease> releases = Array.Empty<DriverRelease>();
        if (nvidia is not null)
        {
            try
            {
                releases = await GpuDrivers.NvidiaReleasesAsync(nvidia.Name);
                ReleasesNote = releases.Count == 0 ? "nvidia did not return drivers for this card." : "official whql game ready drivers from nvidia.";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or System.Xml.XmlException)
            {
                ReleasesNote = "could not reach nvidia. check your connection and refresh.";
            }
        }
        else
        {
            ReleasesNote = "release list is nvidia-only for now. amd and intel are next.";
        }

        var newest = releases.FirstOrDefault();
        Newest = newest?.Version ?? "-";
        NewestNote = newest is null ? string.Empty : "released " + newest.Released.ToString("MMM dd yyyy", CultureInfo.InvariantCulture).ToLowerInvariant();

        var measured = summaries.Where(s => s.Matches >= 3 && s.AvgFps is not null).ToList();
        var best = measured.Where(s => s.Crashes == 0).OrderByDescending(s => s.AvgFps).FirstOrDefault();
        var current = summaries.FirstOrDefault(s => s.Version == Installed);

        Releases.Clear();
        foreach (var r in releases)
        {
            var tags = new List<string>();
            if (r.Version == Installed) tags.Add("[installed]");
            if (ReferenceEquals(r, newest)) tags.Add("[newest]");
            if (best is not null && r.Version == best.Version && measured.Count > 1) tags.Add("[your best]");
            Releases.Add(new ReleaseRow(r, string.Join(" ", tags)));
        }

        BuildVerdict(newest, current, best, measured);
        CommandManager.InvalidateRequerySuggested();
    }

    private void BuildVerdict(DriverRelease? newest, DriverSummary? current, DriverSummary? best, List<DriverSummary> measured)
    {
        _primaryAction = null;

        if (current is { Sessions: >= 3 } && current.Crashes * 3 >= current.Sessions)
        {
            Verdict = $"{Installed} crashed your game in {current.Crashes} of {current.Sessions} sessions.";
            var stable = measured.Where(s => s.Version != Installed && s.Crashes == 0).OrderByDescending(s => s.AvgFps).FirstOrDefault();
            VerdictDetail = stable is null
                ? "no other version has enough data yet. a clean install of the newest driver is the usual fix."
                : $"{stable.Version} ran {stable.Matches} matches without a crash. going back is the safest option.";
            _primaryAction = stable is null ? Releases.FirstOrDefault() : Releases.FirstOrDefault(r => r.Version == stable.Version);
        }
        else if (best is not null && current?.AvgFps is { } cur && best.Version != Installed && best.AvgFps > cur * 1.03)
        {
            var gain = (best.AvgFps!.Value - cur) / cur * 100;
            Verdict = $"{best.Version} gave you {gain:0}% more fps than {Installed}.";
            VerdictDetail = $"measured over {best.Matches} vs {current.Matches} matches from your own game logs.";
            _primaryAction = Releases.FirstOrDefault(r => r.Version == best.Version);
        }
        else if (newest is not null && IsNewer(newest.Version, Installed))
        {
            Verdict = $"{newest.Version} is out. you're on {Installed}.";
            VerdictDetail = current is { AvgFps: not null }
                ? $"{Installed} averaged {current.AvgFps:0} fps over {current.Matches} matches. after you update, aeox driver compares both versions automatically."
                : "after you update, aeox driver compares versions from your game logs automatically.";
            _primaryAction = Releases.FirstOrDefault();
        }
        else
        {
            Verdict = $"{Installed} is the newest driver and it's running fine.";
            VerdictDetail = current is { AvgFps: not null }
                ? $"{current.Matches} matches measured, {current.AvgFps:0} fps average, {current.Crashes} crashes."
                : "play a few matches and aeox driver starts measuring.";
        }

        Raise(nameof(PrimaryText));
        Raise(nameof(HasPrimary));
    }

    private static IReadOnlyList<DriverSummary> LoadSummaries()
    {
        var matches = new List<MatchStat>();
        var sessions = new List<GameSession>();
        foreach (var saved in GameCatalog.SavedDirs())
        {
            var history = MatchHistory.Load(AeoxContext.DefaultDataDir(), GameCatalog.HistoryKey(saved));
            history.ImportLogs(new GamePaths(saved).LogDir);
            matches.AddRange(history.Data.Matches);
            sessions.AddRange(history.Data.Sessions);
        }
        return MatchHistory.Summarize(matches, sessions);
    }

    private static bool IsNewer(string a, string b) =>
        double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
        double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
        x > y;
}
