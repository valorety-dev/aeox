using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Aeox.Core.Network;

public sealed record ConnectionInfo(string AdapterName, bool IsWireless, long SpeedMbps, string? Gateway);

public sealed record PingStats(string Host, int Sent, int Received, double AverageMs, double JitterMs, long MaxMs)
{
    public double LossPercent => Sent == 0 ? 0 : 100.0 * (Sent - Received) / Sent;
}

public static partial class NetworkProbe
{
    public static ConnectionInfo? DetectConnection()
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(n => new
            {
                Nic = n,
                Gateway = n.GetIPProperties().GatewayAddresses
                    .Select(g => g.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any))
            })
            .FirstOrDefault(x => x.Gateway is not null);
        if (nic is null) return null;
        return new ConnectionInfo(
            nic.Nic.Description,
            nic.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
            nic.Nic.Speed / 1_000_000,
            nic.Gateway!.ToString());
    }

    public static string? LastServerFromLog(string logPath)
    {
        if (!File.Exists(logPath)) return null;
        using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var take = Math.Min(fs.Length, 4 * 1024 * 1024);
        fs.Seek(-take, SeekOrigin.End);
        using var reader = new StreamReader(fs);
        var text = reader.ReadToEnd();
        var matches = ServerPattern().Matches(text);
        return matches.Count == 0 ? null : matches[^1].Groups[1].Value;
    }

    public static async Task<PingStats> PingAsync(string host, int count, CancellationToken token = default)
    {
        using var ping = new Ping();
        var times = new List<long>();
        for (var i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(host, 1000);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
            }
            catch (PingException)
            {
            }
            await Task.Delay(150, token);
        }
        var avg = times.Count > 0 ? times.Average() : 0;
        var jitter = times.Count > 1 ? times.Zip(times.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
        return new PingStats(host, count, times.Count, avg, jitter, times.Count > 0 ? times.Max() : 0);
    }

    public static string Verdict(ConnectionInfo? connection, PingStats? router, PingStats? server)
    {
        if (router is null) return "No network connection found.";
        var localBad = router.LossPercent > 0 || router.MaxMs > 20 || router.JitterMs > 3;
        if (localBad)
            return connection is { IsWireless: true }
                ? "Your Wi-Fi is the weak spot: the router itself spikes or drops. A LAN cable fixes this."
                : "Your own network spikes or drops at the router. Check the cable and the router.";
        if (server is null) return "Your connection to the router is clean. Play a match so Aeox can test the Retrac server too.";
        if (server.Received == 0) return "The Retrac server does not answer pings, so only the router test is meaningful. Your side looks clean.";
        if (server.LossPercent > 2) return "Packet loss happens past your router, at your provider or the Retrac server. Nothing on your PC causes it.";
        if (server.JitterMs > 5) return "Ping to the server is uneven. That usually comes from your provider or the route, not your PC.";
        return connection is { IsWireless: true }
            ? "Connection looks healthy. A LAN cable would still make ping a little steadier."
            : "Connection looks healthy.";
    }

    [GeneratedRegex(@"LogNet: Browse: (\d{1,3}(?:\.\d{1,3}){3}):\d+")]
    private static partial Regex ServerPattern();
}
