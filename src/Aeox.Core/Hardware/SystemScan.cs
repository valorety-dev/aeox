using System.Globalization;
using System.Management;
using Aeox.Core.Game;
using Aeox.Core.Network;
using Aeox.Core.Stats;
using Aeox.Core.Windows;
using Microsoft.Win32;

namespace Aeox.Core.Hardware;

public sealed record PartInfo(string Kind, string Title, string Detail, string Status, string Note);

public sealed record ScanResult(
    IReadOnlyList<PartInfo> Parts,
    string Bottleneck,
    string Verdict,
    int MatchesUsed,
    double? AvgFps);

public static class SystemScan
{
    public static ScanResult Run(string dataDir)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { return Build(dataDir); }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private static ScanResult Build(string dataDir)
    {
        var hw = HardwareInfo.Detect();
        var matches = new List<MatchStat>();
        foreach (var kind in new[] { GameKind.Retrac, GameKind.Fortnite })
        {
            var history = MatchHistory.Load(dataDir, kind);
            history.ImportLogs(GameProfile.For(kind).Paths.LogDir);
            matches.AddRange(history.Data.Matches);
        }
        var recent = matches.OrderBy(m => m.TimeUtc).TakeLast(10)
            .Where(m => m.GameThreadMs is not null && m.RenderThreadMs is not null && m.GpuMs is not null)
            .ToList();

        double? gt = recent.Count > 0 ? recent.Average(m => m.GameThreadMs!.Value) : null;
        double? rt = recent.Count > 0 ? recent.Average(m => m.RenderThreadMs!.Value) : null;
        double? gpu = recent.Count > 0 ? recent.Average(m => m.GpuMs!.Value) : null;
        double? fps = recent.Count > 0 ? recent.Average(m => m.AvgFps) : null;
        double? jitter = recent.Where(m => m.JitterMs is not null).Select(m => m.JitterMs!.Value).DefaultIfEmpty().Average();
        if (jitter == 0) jitter = null;

        var cpuMs = gt is null ? (double?)null : Math.Max(gt.Value, rt!.Value);
        var bottleneck = "none";
        if (cpuMs is not null && gpu is not null)
        {
            if (cpuMs > gpu * 1.15) bottleneck = "cpu";
            else if (gpu > cpuMs * 1.15) bottleneck = "gpu";
            else bottleneck = "balanced";
        }

        var parts = new List<PartInfo>();
        var (cores, threads, clock) = CpuDetails();
        parts.Add(new PartInfo("cpu", Short(hw.CpuName),
            $"{cores} cores / {threads} threads · {clock / 1000.0:0.0} ghz" + (hw.IsX3D ? " · 3d v-cache" : string.Empty),
            bottleneck == "cpu" ? "limit" : "ok",
            cpuMs is null ? "no match data yet" : $"{cpuMs:0.0} ms per frame (game {gt:0.0}, render {rt:0.0})"));

        var gpuName = hw.Gpus.FirstOrDefault(g => g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || g.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase))
                      ?? hw.Gpus.FirstOrDefault() ?? "unknown gpu";
        var vram = VideoMemoryGb(gpuName);
        parts.Add(new PartInfo("gpu", Short(gpuName),
            vram > 0 ? $"{vram:0} gb vram" : "dedicated graphics",
            bottleneck == "gpu" ? "limit" : "ok",
            gpu is null ? "no match data yet" : $"{gpu:0.0} ms per frame"));

        var (ramGb, ramSpeed, sticks, jedec) = MemoryDetails();
        parts.Add(new PartInfo("ram", $"{ramGb} gb",
            $"{sticks} sticks · {ramSpeed} mt/s",
            jedec ? "warn" : "ok",
            jedec ? "running at default speed, expo/xmp looks off" : "running at rated speed"));

        var (diskName, diskType) = GameDisk();
        parts.Add(new PartInfo("ssd", Short(diskName), diskType,
            diskType.Contains("hdd", StringComparison.OrdinalIgnoreCase) ? "warn" : "ok",
            diskType.Contains("hdd", StringComparison.OrdinalIgnoreCase) ? "games on a hard drive load and stream slower" : "fast enough for streaming assets"));

        var plan = PowerPlans.Name(PowerPlans.GetActive()).ToLowerInvariant();
        parts.Add(new PartInfo("monitor", $"{hw.ScreenWidth}×{hw.ScreenHeight}",
            $"{hw.RefreshRate} hz",
            fps is not null && fps < hw.RefreshRate * 0.9 ? "warn" : "ok",
            fps is null ? $"power plan: {plan}" : $"{fps:0} fps average vs {hw.RefreshRate} hz · power plan: {plan}"));

        var net = NetworkProbe.DetectConnection();
        var wireless = net?.IsWireless ?? false;
        parts.Add(new PartInfo("network", net is null ? "offline" : wireless ? "wi-fi" : "ethernet",
            net is null ? "no connection" : $"{net.SpeedMbps} mbps",
            wireless || jitter > 3 ? "warn" : "ok",
            jitter is null ? (wireless ? "wi-fi adds jitter, a cable is steadier" : "wired") : $"{jitter:0.0} ms jitter in your matches"));

        var verdict = bottleneck switch
        {
            "cpu" => $"your cpu sets the pace. it needs {cpuMs:0.0} ms per frame while the gpu waits at {gpu:0.0} ms.",
            "gpu" => $"your gpu sets the pace at {gpu:0.0} ms per frame. lower render scale or resolution for more fps.",
            "balanced" => "cpu and gpu are evenly loaded. this setup is well matched.",
            _ => "play a few matches and aeox scan can tell which part holds you back."
        };

        return new ScanResult(parts, bottleneck, verdict, recent.Count, fps);
    }

    private static string Short(string name) => name
        .Replace("(R)", string.Empty).Replace("(TM)", string.Empty).Replace("NVIDIA ", string.Empty)
        .Replace(" Processor", string.Empty).Replace("  ", " ").Trim().ToLowerInvariant();

    private static (int Cores, int Threads, int ClockMhz) CpuDetails()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");
            foreach (var o in s.Get())
                return (Convert.ToInt32(o["NumberOfCores"]), Convert.ToInt32(o["NumberOfLogicalProcessors"]), Convert.ToInt32(o["MaxClockSpeed"]));
        }
        catch (Exception)
        {
        }
        return (Environment.ProcessorCount / 2, Environment.ProcessorCount, 0);
    }

    private static double VideoMemoryGb(string gpuName)
    {
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (cls is null) return 0;
            foreach (var sub in cls.GetSubKeyNames())
            {
                using var key = cls.OpenSubKey(sub);
                if (key?.GetValue("DriverDesc") is not string desc || !string.Equals(desc.Trim(), gpuName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (key.GetValue("HardwareInformation.qwMemorySize") is long bytes) return bytes / 1024.0 / 1024 / 1024;
                if (key.GetValue("HardwareInformation.qwMemorySize") is byte[] raw && raw.Length >= 8) return BitConverter.ToInt64(raw, 0) / 1024.0 / 1024 / 1024;
            }
        }
        catch (Exception)
        {
        }
        return 0;
    }

    private static (long Gb, int Speed, int Sticks, bool Jedec) MemoryDetails()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Capacity, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            var sticks = s.Get().Cast<ManagementObject>().ToList();
            if (sticks.Count == 0) return (0, 0, 0, false);
            var gb = sticks.Sum(x => Convert.ToInt64(x["Capacity"] ?? 0L)) / (1024L * 1024 * 1024);
            var speed = sticks.Select(x => Convert.ToInt32(x["ConfiguredClockSpeed"] ?? 0)).Min();
            var ddr5 = Convert.ToInt32(sticks[0]["SMBIOSMemoryType"] ?? 0) == 34;
            return (gb, speed, sticks.Count, ddr5 ? speed <= 4800 : speed <= 2666);
        }
        catch (Exception)
        {
            return (0, 0, 0, false);
        }
    }

    private static (string Name, string Type) GameDisk()
    {
        try
        {
            using var s = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT FriendlyName, MediaType, BusType FROM MSFT_PhysicalDisk");
            var disks = s.Get().Cast<ManagementObject>().ToList();
            var best = disks.OrderByDescending(d => Convert.ToInt32(d["BusType"] ?? 0) == 17).FirstOrDefault();
            if (best is null) return ("unknown drive", "ssd");
            var media = Convert.ToInt32(best["MediaType"] ?? 0);
            var bus = Convert.ToInt32(best["BusType"] ?? 0);
            var type = bus == 17 ? "nvme ssd" : media == 3 ? "hdd" : media == 4 ? "sata ssd" : "drive";
            return (best["FriendlyName"] as string ?? "drive", type);
        }
        catch (Exception)
        {
            return ("drive", "ssd");
        }
    }
}
