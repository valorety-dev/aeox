using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Aeox.Core.Changes;
using Aeox.Core.Game;
using Aeox.Core.Hardware;
using Aeox.Core.Tweaks;
using Aeox.Core.Windows;

namespace Aeox.App.ViewModels;

public enum StatusKind
{
    Success,
    Pending,
    Warning,
    Error
}

public sealed class MainViewModel : Observable
{
    private IReadOnlyList<PlannedChange> _plan = Array.Empty<PlannedChange>();
    private string _page = "Checkup";
    private StatusKind _status = StatusKind.Success;
    private string _statusTitle = string.Empty;
    private string _statusDetail = string.Empty;
    private int _justApplied;
    private string? _justRestored;

    private readonly HardwareInfo _hardware;

    public MainViewModel()
    {
        Settings = AppSettings.Load();
        _hardware = HardwareInfo.Detect();
        Engine = new ChangeEngine(new OriginalStore(System.IO.Path.Combine(AeoxContext.DefaultDataDir(), "originals.json")));
        Ctx = null!;
        Pages = Array.Empty<PageViewModel>();
        Network = null!;
        BuildForGame(Settings.Game);
        Checkup = new CheckupViewModel(() => Ctx, p => Page = p);
        Stats = new StatsViewModel(() => Ctx);
        _ = Checkup.RunAsync();

        ApplyCommand = new RelayCommand(Apply, () => _plan.Count > 0);
        RestoreCommand = new RelayCommand(RestoreAll, () => Engine.Store.All.Count > 0);
        NavigateCommand = new ParamCommand(p => Page = p ?? "Performance");
        Refresh();
    }

    public AppSettings Settings { get; }
    public AeoxContext Ctx { get; private set; }
    public ChangeEngine Engine { get; }
    public IReadOnlyList<PageViewModel> Pages { get; private set; }
    public NetworkViewModel Network { get; private set; }
    public CheckupViewModel Checkup { get; }
    public StatsViewModel Stats { get; }

    public bool IsRetrac
    {
        get => Ctx.Game.Kind == GameKind.Retrac;
        set { if (value) SelectGame(GameKind.Retrac); }
    }

    public bool IsFortnite
    {
        get => Ctx.Game.Kind == GameKind.Fortnite;
        set { if (value) SelectGame(GameKind.Fortnite); }
    }

    public string GameText => Ctx.Game.Name + (Ctx.GameExe is null ? "  ·  game files not found" : string.Empty);

    private void SelectGame(GameKind kind)
    {
        if (Ctx.Game.Kind == kind) return;
        Settings.Game = kind;
        Settings.Save();
        BuildForGame(kind);
        if (Page == "Stats") _ = Stats.LoadAsync();
        _justApplied = 0;
        _justRestored = null;
        Refresh();
    }

    private void BuildForGame(GameKind kind)
    {
        Ctx = new AeoxContext(GameProfile.For(kind), _hardware, AeoxContext.DefaultDataDir());
        Pages = new[]
        {
            BuildPage(TweakCategory.Performance, "Performance", "03", "more fps, less delay."),
            BuildPage(TweakCategory.Visuals, "Visuals", "04", "how the game is displayed."),
            BuildPage(TweakCategory.System, "System", "06", "windows settings that affect the game.")
        };
        Network = new NetworkViewModel(Ctx);
        Raise(nameof(Ctx));
        Raise(nameof(Pages));
        Raise(nameof(Network));
        Raise(nameof(CurrentPage));
        Raise(nameof(IsRetrac));
        Raise(nameof(IsFortnite));
        Raise(nameof(GameText));
        Raise(nameof(ConfigText));
    }
    public ObservableCollection<PreviewLine> Preview { get; } = new();
    public ICommand ApplyCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand NavigateCommand { get; }

    private IEnumerable<ISettingItem> AllItems => Pages.SelectMany(p => p.Items.OfType<ISettingItem>());

    public string Page
    {
        get => _page;
        set
        {
            if (!Set(ref _page, value)) return;
            Raise(nameof(CurrentPage));
            Raise(nameof(IsTweakPage));
            Raise(nameof(IsNetworkPage));
            Raise(nameof(IsSettingsPage));
            Raise(nameof(IsCheckupPage));
            Raise(nameof(IsStatsPage));
            if (value == "Stats") _ = Stats.LoadAsync();
            if (value == "Checkup" && Checkup is { HasRun: true }) _ = Checkup.RunAsync();
        }
    }

    public PageViewModel? CurrentPage => Pages.FirstOrDefault(p => p.Key == Page);
    public bool IsTweakPage => CurrentPage is not null;
    public bool IsNetworkPage => Page == "Network";
    public bool IsSettingsPage => Page == "Settings";
    public bool IsCheckupPage => Page == "Checkup";
    public bool IsStatsPage => Page == "Stats";

    public StatusKind Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => Set(ref _statusTitle, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => Set(ref _statusDetail, value);
    }

    public bool HasPending => _plan.Count > 0;

    public string CpuText => Ctx.Hardware.CpuName + (Ctx.Hardware.IsX3D ? "  ·  3D V-Cache" : string.Empty);
    public string GpuText => Ctx.Hardware.Gpus.Count > 0 ? string.Join("  ·  ", Ctx.Hardware.Gpus) : "Not detected";
    public string DisplayText => $"{Ctx.Hardware.ScreenWidth}×{Ctx.Hardware.ScreenHeight} at {Ctx.Hardware.RefreshRate} Hz";
    public string ConfigText => Ctx.Paths.ConfigDir;
    public string DataText => Ctx.DataDir;
    public string BackupText => Engine.Store.All.Count == 0
        ? "Nothing changed yet."
        : $"{Engine.Store.All.Count} original values saved. Restore puts every one of them back.";

    private PageViewModel BuildPage(TweakCategory category, string title, string number, string subtitle)
    {
        var items = new List<object>();
        items.AddRange(TweakCatalog.ChoicesFor(category).Where(c => c.IsSupported(Ctx)).Select(c => new ChoiceItem(c, Ctx, Engine, OnItemChanged)));
        items.AddRange(TweakCatalog.For(category)
            .Where(t => t.IsSupported(Ctx))
            .Select(t => new TweakItem(t, Ctx, Engine.IsApplied(t.Changes(Ctx)), OnItemChanged)));
        return new PageViewModel(title, number, title.ToLowerInvariant(), subtitle, items);
    }

    private void OnItemChanged()
    {
        _justApplied = 0;
        _justRestored = null;
        Refresh();
    }

    public void Refresh()
    {
        _plan = Engine.Plan(AllItems.SelectMany(i => i.Desired()));
        BuildPreview();
        UpdateStatus();
        Raise(nameof(HasPending));
        Raise(nameof(BackupText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateStatus()
    {
        if (!Ctx.Paths.ConfigExists)
        {
            SetStatus(StatusKind.Error, $"{Ctx.Game.ShortName} settings not found", $"Launch {Ctx.Game.ShortName} once so the game creates its settings files.");
            return;
        }
        if (_plan.Count > 0)
        {
            if (RetracGame.IsGameRunning())
                SetStatus(StatusKind.Warning, "Close Fortnite to apply", "Aeox writes the settings while the game is closed.");
            else
                SetStatus(StatusKind.Pending, $"{_plan.Count} change{(_plan.Count == 1 ? "" : "s")} ready", "Takes effect next time you launch Retrac.");
            return;
        }
        if (_justRestored is not null)
        {
            SetStatus(StatusKind.Success, "Restored", _justRestored);
            return;
        }
        if (_justApplied > 0)
        {
            SetStatus(StatusKind.Success, "Applied", $"{_justApplied} change{(_justApplied == 1 ? "" : "s")} written. Active next launch.");
            return;
        }
        var items = AllItems.ToList();
        var active = items.Count(i => i.IsActive);
        SetStatus(StatusKind.Success, "Up to date", active == 0 ? "Nothing active yet." : $"{active} of {items.Count} settings active.");
    }

    private void SetStatus(StatusKind kind, string title, string detail)
    {
        Status = kind;
        StatusTitle = title;
        StatusDetail = detail;
    }

    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set { Settings.StartWithWindows = value; Settings.Save(); AutoMode.SetStartWithWindows(value); Raise(); }
    }

    public bool CloseToTray
    {
        get => Settings.CloseToTray;
        set { Settings.CloseToTray = value; Settings.Save(); Raise(); }
    }

    public bool AutoReapply
    {
        get => Settings.AutoReapply;
        set { Settings.AutoReapply = value; Settings.Save(); Raise(); }
    }

    public bool SessionReports
    {
        get => Settings.SessionReports;
        set { Settings.SessionReports = value; Settings.Save(); Raise(); }
    }

    private void RememberActive()
    {
        var prefix = Ctx.Game.Kind + "|";
        Settings.ActiveIds.RemoveAll(i => i.StartsWith(prefix, StringComparison.Ordinal));
        foreach (var item in AllItems)
        {
            if (item is TweakItem { IsActive: true } t) Settings.ActiveIds.Add(prefix + "tweak:" + t.Tweak.Id);
            if (item is ChoiceItem { ActiveOption: { } option } c) Settings.ActiveIds.Add(prefix + "choice:" + c.Setting.Id + ":" + option.Label);
        }
        Settings.Save();
    }

    public int ReapplyRemembered()
    {
        if (RetracGame.IsGameRunning() || !Ctx.Paths.ConfigExists) return 0;
        var prefix = Ctx.Game.Kind + "|";
        var desired = new List<(string, IReadOnlyList<Change>, bool)>();
        foreach (var id in Settings.ActiveIds.Where(i => i.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var parts = id[prefix.Length..].Split(':', 3);
            if (parts[0] == "tweak")
            {
                var tweak = TweakCatalog.All.FirstOrDefault(t => t.Id == parts[1]);
                if (tweak is not null && tweak.IsSupported(Ctx)) desired.Add((tweak.Title, tweak.Changes(Ctx), true));
            }
            else if (parts[0] == "choice" && parts.Length == 3)
            {
                var setting = TweakCatalog.Choices.FirstOrDefault(c => c.Id == parts[1]);
                var option = setting?.Options.FirstOrDefault(o => o.Label == parts[2]);
                if (setting is not null && option is not null && setting.IsSupported(Ctx)) desired.Add(($"{setting.Title}: {option.Label}", option.Changes(Ctx), true));
            }
        }
        var plan = Engine.Plan(desired);
        if (plan.Count == 0) return 0;
        try
        {
            Engine.Apply(plan);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return 0;
        }
        ResyncAll();
        Refresh();
        return plan.Count;
    }

    private void ResyncAll()
    {
        foreach (var item in AllItems) item.Resync(Ctx, Engine);
    }

    private void Apply()
    {
        if (_plan.Count == 0) return;
        if (RetracGame.IsGameRunning())
        {
            SetStatus(StatusKind.Warning, "Close Fortnite to apply", "Aeox writes the settings while the game is closed.");
            return;
        }
        try
        {
            var count = _plan.Count;
            var sources = string.Join(", ", _plan.Select(p => p.Source).Distinct());
            Engine.Apply(_plan);
            Stats.RecordApply(sources);
            ResyncAll();
            RememberActive();
            _justApplied = count;
            _justRestored = null;
            Refresh();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, "Could not apply", ex.Message);
        }
    }

    private void RestoreAll()
    {
        if (RetracGame.IsGameRunning())
        {
            SetStatus(StatusKind.Warning, "Close Fortnite to restore", "Aeox restores the settings while the game is closed.");
            return;
        }
        var plan = Engine.PlanRestoreAll();
        var answer = MessageBox.Show(
            $"Put back the {Engine.Store.All.Count} original values Aeox saved before changing anything?",
            "Restore originals", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            Engine.Apply(plan);
            Engine.ForgetOriginals();
            Settings.ActiveIds.Clear();
            Settings.Save();
            ResyncAll();
            _justApplied = 0;
            _justRestored = $"{plan.Count} value{(plan.Count == 1 ? "" : "s")} put back exactly as they were before Aeox.";
            Refresh();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, "Could not restore", ex.Message);
        }
    }

    private void BuildPreview()
    {
        var lines = new List<(List<PreviewToken> Tokens, bool Gap)>();

        void Add(bool gap, params PreviewToken[] tokens) => lines.Add((tokens.ToList(), gap));

        if (_plan.Count == 0)
        {
            Add(false, new PreviewToken("# No pending changes", TokenKind.Comment));
            Add(false, new PreviewToken("# Change a setting to preview what Aeox writes.", TokenKind.Comment));
            var active = AllItems.Where(i => i.IsActive).ToList();
            if (active.Count > 0)
            {
                Add(true, new PreviewToken("# Active right now", TokenKind.Comment));
                foreach (var i in active) Add(false, new PreviewToken("+ ", TokenKind.Muted), new PreviewToken(i.Title, TokenKind.String));
            }
        }

        var reverting = AllItems.Where(i => i.IsReverting).Select(i => i.Title).ToHashSet();
        foreach (var bySource in _plan.GroupBy(p => p.Source))
        {
            var verb = reverting.Contains(bySource.Key) ? "Revert" : "Set";
            Add(lines.Count > 0, new PreviewToken($"# {verb} {bySource.Key}", TokenKind.Comment));

            foreach (var byTarget in bySource.GroupBy(p => (p.Change.Kind, p.Change.Target, p.Change.Section)))
            {
                var first = byTarget.First().Change;
                switch (first.Kind)
                {
                    case ChangeKind.FileReadOnly:
                        foreach (var p in byTarget)
                        {
                            Add(false,
                                new PreviewToken("attrib ", TokenKind.Keyword),
                                new PreviewToken(p.NewValue == "true" ? "+R " : "-R ", TokenKind.Text),
                                new PreviewToken($"\"{p.Change.TargetName}\"", TokenKind.String));
                        }
                        break;

                    case ChangeKind.PowerPlan:
                        foreach (var p in byTarget)
                        {
                            Add(false,
                                new PreviewToken("powercfg ", TokenKind.Keyword),
                                new PreviewToken("/setactive ", TokenKind.Text),
                                new PreviewToken(PowerPlans.Name(p.NewValue), TokenKind.String),
                                new PreviewToken($"   was {PowerPlans.Name(p.OldValue)}", TokenKind.Muted));
                        }
                        break;

                    case ChangeKind.Registry:
                        Add(false,
                            new PreviewToken("reg ", TokenKind.Keyword),
                            new PreviewToken($"{first.Target}", TokenKind.String));
                        foreach (var p in byTarget)
                        {
                            var name = p.Change.Key.Length > 34 ? "…" + p.Change.Key[^33..] : p.Change.Key;
                            if (p.NewValue is null)
                            {
                                Add(false,
                                    new PreviewToken("    ", TokenKind.Text),
                                    new PreviewToken(name, TokenKind.Removed),
                                    new PreviewToken("   deleted, Windows default", TokenKind.Muted));
                            }
                            else
                            {
                                Add(false,
                                    new PreviewToken($"    {name}", TokenKind.Text),
                                    new PreviewToken(" = ", TokenKind.Muted),
                                    new PreviewToken(StripType(p.NewValue), TokenKind.String),
                                    new PreviewToken(p.OldValue is null ? "   new" : $"   was {StripType(p.OldValue)}", TokenKind.Muted));
                            }
                        }
                        break;

                    default:
                        Add(false,
                            new PreviewToken(first.TargetName, TokenKind.Keyword),
                            new PreviewToken("  ", TokenKind.Muted),
                            new PreviewToken($"[{first.Section}]", TokenKind.String));
                        foreach (var p in byTarget)
                        {
                            var note = p.Revert switch
                            {
                                RevertKind.Original => "   your original",
                                RevertKind.GameDefault => "   game default",
                                _ => p.OldValue is null ? "   new" : $"   was {p.OldValue}"
                            };
                            if (p.NewValue is null)
                            {
                                Add(false,
                                    new PreviewToken("    ", TokenKind.Text),
                                    new PreviewToken($"{p.Change.Key}={p.OldValue}", TokenKind.Removed),
                                    new PreviewToken(p.Revert == RevertKind.GameDefault ? "   removed, game default" : "   removed", TokenKind.Muted));
                            }
                            else
                            {
                                Add(false,
                                    new PreviewToken($"    {p.Change.Key}", TokenKind.Text),
                                    new PreviewToken(" = ", TokenKind.Muted),
                                    new PreviewToken(p.NewValue, TokenKind.String),
                                    new PreviewToken(note, TokenKind.Muted));
                            }
                        }
                        break;
                }
            }
        }

        Preview.Clear();
        var n = 1;
        foreach (var (tokens, gap) in lines) Preview.Add(new PreviewLine(n++, tokens, gap));
    }

    private static string StripType(string value) =>
        value.StartsWith("dword:", StringComparison.OrdinalIgnoreCase) ? value[6..]
        : value.StartsWith("sz:", StringComparison.OrdinalIgnoreCase) ? value[3..]
        : value;
}

public sealed class ParamCommand : ICommand
{
    private readonly Action<string?> _run;

    public ParamCommand(Action<string?> run)
    {
        _run = run;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _run(parameter as string);
}
