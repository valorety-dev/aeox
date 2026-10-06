using System.Windows.Input;
using Aeox.Core.Network;
using Aeox.Core.Tweaks;

namespace Aeox.App.ViewModels;

public sealed class NetworkViewModel : Observable
{
    private readonly AeoxContext _ctx;
    private bool _isRunning;
    private string _routerText = "Not tested yet";
    private string _serverText = "Not tested yet";
    private string _verdict = "Run the test while nothing else is downloading.";
    private string _connectionText = string.Empty;
    private string _serverLabel = "Retrac server";

    public NetworkViewModel(AeoxContext ctx)
    {
        _ctx = ctx;
        RunCommand = new RelayCommand(() => _ = RunAsync(), () => !IsRunning);
        DescribeConnection(NetworkProbe.DetectConnection());
    }

    public ICommand RunCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value)) Raise(nameof(ButtonText));
        }
    }

    public string ButtonText => IsRunning ? "Testing..." : "Run test";

    public string ConnectionText
    {
        get => _connectionText;
        private set => Set(ref _connectionText, value);
    }

    public string RouterText
    {
        get => _routerText;
        private set => Set(ref _routerText, value);
    }

    public string ServerLabel
    {
        get => _serverLabel;
        private set => Set(ref _serverLabel, value);
    }

    public string ServerText
    {
        get => _serverText;
        private set => Set(ref _serverText, value);
    }

    public string Verdict
    {
        get => _verdict;
        private set => Set(ref _verdict, value);
    }

    private void DescribeConnection(ConnectionInfo? c)
    {
        ConnectionText = c is null
            ? "No active connection"
            : $"{(c.IsWireless ? "Wi-Fi" : "Ethernet")}  ·  {c.SpeedMbps} Mbps  ·  {c.AdapterName}";
    }

    private async Task RunAsync()
    {
        IsRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var connection = NetworkProbe.DetectConnection();
            DescribeConnection(connection);
            var server = NetworkProbe.LastServerFromLog(_ctx.Paths.GameLog);
            ServerLabel = server is null ? "Retrac server" : $"Retrac server  {server}";
            RouterText = "Testing...";
            ServerText = server is null ? "No recent match found in the game log" : "Testing...";

            var routerTask = connection?.Gateway is null ? Task.FromResult<PingStats?>(null) : PingOrNull(connection.Gateway);
            var serverTask = server is null ? Task.FromResult<PingStats?>(null) : PingOrNull(server);
            await Task.WhenAll(routerTask, serverTask);

            RouterText = Describe(routerTask.Result);
            if (server is not null) ServerText = Describe(serverTask.Result);
            Verdict = NetworkProbe.Verdict(connection, routerTask.Result, serverTask.Result);
        }
        finally
        {
            IsRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static async Task<PingStats?> PingOrNull(string host) => await NetworkProbe.PingAsync(host, 30);

    private static string Describe(PingStats? s)
    {
        if (s is null) return "Not available";
        if (s.Received == 0) return "No reply";
        return $"{s.AverageMs:0} ms average   ·   {s.JitterMs:0.0} ms jitter   ·   {s.MaxMs} ms max   ·   {s.LossPercent:0.#}% loss";
    }
}
