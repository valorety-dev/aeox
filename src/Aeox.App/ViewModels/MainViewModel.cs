using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Aeox.Core.Changes;
using Aeox.Core.Game;
using Aeox.Core.Hardware;
using Aeox.Core.Tweaks;

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
    private string _page = "Performance";
    private StatusKind _status = StatusKind.Success;
    private string _statusTitle = string.Empty;
    private string _statusDetail = string.Empty;
    private int _justApplied;
    private string? _justRestored;

    public MainViewModel()
    {
        Ctx = new AeoxContext(RetracPaths.Default(), HardwareInfo.Detect(), AeoxContext.DefaultDataDir());
        Engine = new ChangeEngine(new OriginalStore(System.IO.Path.Combine(Ctx.DataDir, "originals.json")));
        PerformanceTweaks = new ObservableCollection<TweakItem>(
            TweakCatalog.For(TweakCategory.Performance)
                .Where(t => t.IsSupported(Ctx))
                .Select(t => new TweakItem(t, Ctx, Engine.IsApplied(t.Changes(Ctx)), OnTweakToggled)));
        ApplyCommand = new RelayCommand(Apply, () => _plan.Count > 0);
        RestoreCommand = new RelayCommand(RestoreAll, () => Engine.Store.All.Count > 0);
        NavigateCommand = new ParamCommand(p => Page = p ?? "Performance");
        Refresh();
    }

    public AeoxContext Ctx { get; }
    public ChangeEngine Engine { get; }
    public ObservableCollection<TweakItem> PerformanceTweaks { get; }
    public ObservableCollection<PreviewLine> Preview { get; } = new();
    public ICommand ApplyCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand NavigateCommand { get; }

    public string Page
    {
        get => _page;
        set
        {
            if (!Set(ref _page, value)) return;
            Raise(nameof(IsPerformancePage));
            Raise(nameof(IsSettingsPage));
            Raise(nameof(IsComingSoonPage));
        }
    }

    public bool IsPerformancePage => Page == "Performance";
    public bool IsSettingsPage => Page == "Settings";
    public bool IsComingSoonPage => !IsPerformancePage && !IsSettingsPage;

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
    public string ConfigText => Ctx.Paths.ConfigDir;
    public string DataText => Ctx.DataDir;
    public string BackupText => Engine.Store.All.Count == 0
        ? "Nothing changed yet."
        : $"{Engine.Store.All.Count} original values saved. Restore puts every one of them back.";

    private void OnTweakToggled()
    {
        _justApplied = 0;
        _justRestored = null;
        Refresh();
    }

    public void Refresh()
    {
        _plan = Engine.Plan(PerformanceTweaks.Select(t => (t.Title, t.Changes, t.IsOn)));
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
            SetStatus(StatusKind.Error, "Retrac settings not found", "Launch Retrac once so Fortnite creates its settings files, then reopen Aeox.");
            return;
        }
        if (_plan.Count > 0)
        {
            if (RetracGame.IsGameRunning())
                SetStatus(StatusKind.Warning, "Close Fortnite to apply", "Aeox writes the settings while the game is closed.");
            else
                SetStatus(StatusKind.Pending, $"{_plan.Count} change{(_plan.Count == 1 ? "" : "s")} ready", "Review the preview, then apply. Takes effect next time you launch Retrac.");
            return;
        }
        if (_justRestored is not null)
        {
            SetStatus(StatusKind.Success, "Restored", _justRestored);
            return;
        }
        if (_justApplied > 0)
        {
            SetStatus(StatusKind.Success, "Applied successfully", $"{_justApplied} change{(_justApplied == 1 ? "" : "s")} written. Active next time you launch Retrac.");
            return;
        }
        var active = PerformanceTweaks.Count(t => t.IsApplied);
        SetStatus(StatusKind.Success, "Up to date", active == 0 ? "No tweaks active yet." : $"{active} of {PerformanceTweaks.Count} performance tweaks are active.");
    }

    private void SetStatus(StatusKind kind, string title, string detail)
    {
        Status = kind;
        StatusTitle = title;
        StatusDetail = detail;
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
            Engine.Apply(_plan);
            foreach (var t in PerformanceTweaks) t.Sync(Engine.IsApplied(t.Changes));
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
            foreach (var t in PerformanceTweaks) t.Sync(Engine.IsApplied(t.Changes));
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
            Add(false, new PreviewToken("# Flip a toggle to preview what Aeox writes.", TokenKind.Comment));
            var active = PerformanceTweaks.Where(t => t.IsApplied).ToList();
            if (active.Count > 0)
            {
                Add(true, new PreviewToken("# Active right now", TokenKind.Comment));
                foreach (var t in active) Add(false, new PreviewToken("+ ", TokenKind.Muted), new PreviewToken(t.Title, TokenKind.String));
            }
        }

        foreach (var bySource in _plan.GroupBy(p => p.Source))
        {
            var item = PerformanceTweaks.FirstOrDefault(t => t.Title == bySource.Key);
            var verb = item is { IsOn: false } ? "Revert" : "Enable";
            Add(lines.Count > 0, new PreviewToken($"# {verb} {bySource.Key}", TokenKind.Comment));

            foreach (var byTarget in bySource.GroupBy(p => (p.Change.Kind, p.Change.Target, p.Change.Section)))
            {
                var first = byTarget.First().Change;
                if (first.Kind == ChangeKind.FileReadOnly)
                {
                    foreach (var p in byTarget)
                    {
                        var on = p.NewValue == "true";
                        Add(false,
                            new PreviewToken("attrib ", TokenKind.Keyword),
                            new PreviewToken(on ? "+R " : "-R ", TokenKind.Text),
                            new PreviewToken($"\"{p.Change.TargetName}\"", TokenKind.String));
                    }
                    continue;
                }

                if (first.Kind == ChangeKind.Registry)
                {
                    foreach (var p in byTarget)
                    {
                        Add(false,
                            new PreviewToken(p.NewValue is null ? "reg delete " : "reg add ", TokenKind.Keyword),
                            new PreviewToken($"\"{p.Change.Target}\"", TokenKind.String));
                        Add(false,
                            new PreviewToken($"    /v {p.Change.Key}", TokenKind.Text),
                            new PreviewToken(p.NewValue is null ? string.Empty : $" /d {p.NewValue}", TokenKind.String));
                    }
                    continue;
                }

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
            }
        }

        Preview.Clear();
        var n = 1;
        foreach (var (tokens, gap) in lines) Preview.Add(new PreviewLine(n++, tokens, gap));
    }
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
