using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using Aeox.Core.Checkup;
using Aeox.Core.Tweaks;

namespace Aeox.App.ViewModels;

public sealed class CheckRow
{
    public CheckRow(CheckResult result, Action<string> navigate)
    {
        Result = result;
        FixCommand = new RelayCommand(() => navigate(result.FixPage!), () => result.FixPage is not null);
    }

    public CheckResult Result { get; }
    public string Title => Result.Title;
    public string Detail => Result.Detail;
    public bool HasFix => Result.FixPage is not null && Result.Status == CheckStatus.Warn;
    public string FixText => $"Open {Result.FixPage}";
    public ICommand FixCommand { get; }

    public Brush DotBrush => (Brush)System.Windows.Application.Current.Resources[Result.Status switch
    {
        CheckStatus.Good => "SuccessBrush",
        CheckStatus.Warn => "WarnBrush",
        _ => "FaintBrush"
    }];
}

public sealed class CheckupViewModel : Observable
{
    private readonly Func<AeoxContext> _ctx;
    private readonly Action<string> _navigate;
    private bool _isRunning;
    private string _summary = "Scanning...";

    public CheckupViewModel(Func<AeoxContext> ctx, Action<string> navigate)
    {
        _ctx = ctx;
        _navigate = navigate;
        RunCommand = new RelayCommand(() => _ = RunAsync(), () => !_isRunning);
    }

    public ObservableCollection<CheckRow> Rows { get; } = new();
    public ICommand RunCommand { get; }
    public bool HasRun { get; private set; }

    public string Summary
    {
        get => _summary;
        private set => Set(ref _summary, value);
    }

    public async Task RunAsync()
    {
        if (_isRunning) return;
        _isRunning = true;
        Summary = "Scanning...";
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var ctx = _ctx();
            var results = await Task.Run(() => HealthChecks.RunAll(ctx));
            Rows.Clear();
            foreach (var r in results.OrderBy(r => r.Status switch { CheckStatus.Warn => 0, CheckStatus.Info => 2, _ => 1 }))
                Rows.Add(new CheckRow(r, _navigate));
            var warns = results.Count(r => r.Status == CheckStatus.Warn);
            var good = results.Count(r => r.Status == CheckStatus.Good);
            Summary = warns == 0
                ? $"All {good} checks look good."
                : $"{warns} thing{(warns == 1 ? "" : "s")} to fix  ·  {good} good";
            HasRun = true;
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
