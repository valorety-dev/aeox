using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Aeox.Core.Checkup;
using Aeox.Core.Game;
using Aeox.Core.Hardware;

namespace Aeox.Core.Live;

public enum LiveAction
{
    None,
    PinCacheCores,
    OpenPage
}

public sealed record LiveFinding(string Title, CheckStatus Status, string Detail, LiveAction Action = LiveAction.None, string? Page = null);

public sealed record LiveReport(string GameName, int ProcessId, IReadOnlyList<LiveFinding> Findings, IReadOnlyList<double> CoreLoad);

public sealed record RunningGame(GameProfile Game, Process Process);

public static partial class GameMonitor
{
    public static RunningGame? FindRunning(IReadOnlyList<GameProfile> games)
    {
        var processes = Process.GetProcesses();
        foreach (var game in games.Where(g => g.IsInstalled))
        {
            var names = ExeNames(game);
            var hit = processes.FirstOrDefault(p => names.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase));
            if (hit is null) continue;
            if (game.IsFortnite && game.FindExe() is { } exe && !SameExe(hit, exe)) continue;
            return new RunningGame(game, hit);
        }
        return null;
    }

    public static async Task<LiveReport> CheckAsync(RunningGame running, HardwareInfo hw, CancellationToken token = default)
    {
        var findings = new List<LiveFinding>();
        var pid = running.Process.Id;

        var gpuInstances = GpuInstancesFor(pid);
        var gpuCounters = gpuInstances.Select(i => (Luid: LuidOf(i), Counter: TryCounter("GPU Engine", "Utilization Percentage", i))).Where(x => x.Counter is not null).ToList();
        foreach (var c in gpuCounters) c.Counter!.NextValue();

        var before = CpuTimes();
        var gpuSamples = new Dictionary<string, double>();
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(1000, token);
            foreach (var g in gpuCounters)
            {
                var v = g.Counter!.NextValue();
                gpuSamples[g.Luid] = gpuSamples.GetValueOrDefault(g.Luid) + v / 4.0;
            }
        }
        var after = CpuTimes();
        var load = CoreLoad(before, after);
        foreach (var g in gpuCounters) g.Counter!.Dispose();

        if (hw.IsDualCcdX3D && load.Count >= 4) findings.Add(CcdFinding(running.Process, load));

        var gpuFinding = GpuFinding(hw, gpuSamples);
        if (gpuFinding is not null) findings.Add(gpuFinding);

        var limit = LimitFinding(load, gpuSamples.Values.DefaultIfEmpty(0).Max());
        if (limit is not null) findings.Add(limit);

        return new LiveReport(running.Game.ShortName, pid, findings, load);
    }

    public static bool PinToCacheCores(int pid, int threads)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            var half = threads / 2;
            p.ProcessorAffinity = (IntPtr)((1L << half) - 1);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static LiveFinding CcdFinding(Process process, IReadOnlyList<double> load)
    {
        var half = load.Count / 2;
        var cache = load.Take(half).Average();
        var other = load.Skip(half).Average();
        long? mask = null;
        try { mask = (long)process.ProcessorAffinity; } catch (Exception) { }
        if (mask is { } m && m == (1L << half) - 1)
            return new LiveFinding("V-Cache cores", CheckStatus.Good, "The game is pinned to the 3D V-Cache cores.");
        if (other > 15 && other > cache * 0.6)
            return new LiveFinding("V-Cache cores", CheckStatus.Warn,
                $"The game is spread over both CPU halves ({cache:0}% on the V-Cache cores, {other:0}% on the others). " +
                "Pin it to the V-Cache cores now, turn on Games on V-Cache cores in windows, or set CPPC Dynamic Preferred Cores to Cache in the BIOS.",
                LiveAction.PinCacheCores);
        return new LiveFinding("V-Cache cores", CheckStatus.Good, $"Most of the load sits on the V-Cache cores ({cache:0}% vs {other:0}%).");
    }

    private static LiveFinding? GpuFinding(HardwareInfo hw, Dictionary<string, double> gpuByLuid)
    {
        if (gpuByLuid.Count == 0)
            return new LiveFinding("Graphics card", CheckStatus.Info, "Windows did not report GPU load for this game, so aeox can't tell which GPU it uses.");
        var used = gpuByLuid.OrderByDescending(kv => kv.Value).First().Key;
        var dedicated = DedicatedLuid();
        if (!hw.HasMultipleGpus || dedicated is null || string.Equals(used, dedicated, StringComparison.OrdinalIgnoreCase))
            return new LiveFinding("Graphics card", CheckStatus.Good, $"The game renders on {(hw.DedicatedGpu ?? "your graphics card")}.");
        return new LiveFinding("Graphics card", CheckStatus.Warn,
            "The game renders on the built-in graphics instead of your graphics card. Turn on Use the dedicated GPU in windows, then restart the game.",
            LiveAction.OpenPage, "System");
    }

    private static LiveFinding? LimitFinding(IReadOnlyList<double> load, double gpu)
    {
        if (load.Count == 0) return null;
        var busiest = load.Max();
        if (gpu >= 90)
            return new LiveFinding("What limits FPS", CheckStatus.Info, $"Your GPU is at {gpu:0}%, so it sets the pace. Lower render scale or resolution for more FPS.");
        if (gpu > 0 && gpu < 75 && busiest > 85)
            return new LiveFinding("What limits FPS", CheckStatus.Info, $"Your GPU waits at {gpu:0}% while one CPU core is at {busiest:0}%. The CPU sets the pace, so close background apps.");
        return gpu > 0 ? new LiveFinding("What limits FPS", CheckStatus.Info, $"GPU at {gpu:0}%, busiest CPU core at {busiest:0}%. Nothing is maxed out, so an FPS cap is probably active.") : null;
    }

    private static string[] ExeNames(GameProfile game)
    {
        if (game.IsFortnite) return new[] { GameRunning.GameProcess };
        var def = OtherGames.All.FirstOrDefault(d => "game-" + d.Key == game.Id);
        return def?.ExeNames.Select(Path.GetFileNameWithoutExtension).Where(n => n is not null).Select(n => n!).ToArray() ?? Array.Empty<string>();
    }

    private static bool SameExe(Process p, string exe)
    {
        try { return string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase); }
        catch (Exception) { return true; }
    }

    private static PerformanceCounter? TryCounter(string category, string counter, string instance)
    {
        try { return new PerformanceCounter(category, counter, instance, true); }
        catch (Exception) { return null; }
    }

    private static IReadOnlyList<string> GpuInstancesFor(int pid)
    {
        try
        {
            return new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                .Where(n => n.StartsWith($"pid_{pid}_", StringComparison.Ordinal) && n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    private static string? DedicatedLuid()
    {
        try
        {
            var cat = new PerformanceCounterCategory("GPU Adapter Memory");
            string? best = null;
            float bestBytes = 0;
            foreach (var instance in cat.GetInstanceNames())
            {
                using var c = TryCounter("GPU Adapter Memory", "Dedicated Usage", instance);
                if (c is null) continue;
                var v = c.NextValue();
                if (v > bestBytes)
                {
                    bestBytes = v;
                    best = LuidOf(instance);
                }
            }
            return bestBytes > 256 * 1024 * 1024 ? best : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string LuidOf(string instance)
    {
        var m = LuidPattern().Match(instance);
        return m.Success ? m.Value.ToLowerInvariant() : instance;
    }

    private static IReadOnlyList<double> CoreLoad(long[][] a, long[][] b)
    {
        var result = new List<double>();
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var idle = b[i][0] - a[i][0];
            var total = (b[i][1] - a[i][1]) + (b[i][2] - a[i][2]);
            result.Add(total <= 0 ? 0 : Math.Clamp(100.0 * (total - idle) / total, 0, 100));
        }
        return result;
    }

    private static long[][] CpuTimes()
    {
        var count = Environment.ProcessorCount;
        var size = Marshal.SizeOf<ProcessorPerformance>();
        var buffer = Marshal.AllocHGlobal(size * count);
        try
        {
            if (NtQuerySystemInformation(8, buffer, size * count, out _) != 0) return Array.Empty<long[]>();
            var result = new long[count][];
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<ProcessorPerformance>(buffer + i * size);
                result[i] = new[] { info.IdleTime, info.KernelTime, info.UserTime };
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPerformance
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr info, int length, out int returnLength);

    [GeneratedRegex(@"luid_0x[0-9a-fA-F]+_0x[0-9a-fA-F]+")]
    private static partial Regex LuidPattern();
}
