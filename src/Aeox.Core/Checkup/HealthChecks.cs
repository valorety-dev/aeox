using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Aeox.Core.Network;
using Aeox.Core.Tweaks;
using Aeox.Core.Windows;
using Microsoft.Win32;

namespace Aeox.Core.Checkup;

public enum CheckStatus
{
    Good,
    Warn,
    Info
}

public sealed record CheckResult(string Title, CheckStatus Status, string Detail, string? FixPage = null);

public static class HealthChecks
{
    private static readonly string[] OptimizerNames =
    {
        "Ultimate Optimizer", "Hone", "Advanced SystemCare", "Wise Game Booster", "Razer Cortex", "Driver Booster",
        "WinOptimizer", "Game Fire", "PC Optimizer", "Optimizer Pro"
    };

    private static readonly (string Process, string Name)[] HeavyBackgroundApps =
    {
        ("NZXT CAM", "NZXT CAM"), ("ArmouryCrate", "Armoury Crate"), ("asus_framework", "Armoury Crate"),
        ("iCUE", "Corsair iCUE"), ("wallpaper32", "Wallpaper Engine"), ("wallpaper64", "Wallpaper Engine"),
        ("RazerAppEngine", "Razer Synapse"), ("lghub", "Logitech G HUB"), ("MSI.CentralServer", "MSI Center"),
        ("OverwolfBrowser", "Overwolf")
    };

    public static IReadOnlyList<CheckResult> RunAll(AeoxContext ctx)
    {
        var results = new List<CheckResult>();
        void Try(Func<CheckResult?> check)
        {
            try
            {
                var r = check();
                if (r is not null) results.Add(r);
            }
            catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or IOException)
            {
            }
        }

        Try(() => RefreshRate(ctx));
        Try(() => PowerPlan(ctx));
        Try(Memory);
        Try(() => DedicatedGpu(ctx));
        Try(Optimizers);
        Try(BackgroundApps);
        Try(Connection);
        Try(MouseAcceleration);
        Try(GameMode);
        Try(GpuScheduling);
        Try(() => DiskSpace(ctx));
        Try(MemoryIntegrity);
        return results;
    }

    private static CheckResult RefreshRate(AeoxContext ctx)
    {
        var current = ctx.Hardware.RefreshRate;
        var max = MaxRefreshAtCurrentResolution();
        if (max > current + 1)
            return new CheckResult("Monitor refresh rate", CheckStatus.Warn,
                $"Running at {current} Hz but your monitor supports {max} Hz. Change it in Windows display settings, Advanced display.");
        return new CheckResult("Monitor refresh rate", CheckStatus.Good, $"{current} Hz, the highest your monitor offers.");
    }

    private static CheckResult PowerPlan(AeoxContext ctx)
    {
        var active = PowerPlans.GetActive();
        var name = PowerPlans.Name(active);
        if (ctx.Hardware.IsX3D)
        {
            return active == PowerPlans.Balanced
                ? new CheckResult("Power plan", CheckStatus.Good, "Balanced, which lets AMD put games on the V-Cache cores.")
                : new CheckResult("Power plan", CheckStatus.Warn, $"{name} stops AMD from steering games to the V-Cache cores. Use Balanced.", "System");
        }
        return active == PowerPlans.PowerSaver
            ? new CheckResult("Power plan", CheckStatus.Warn, "Power saver limits CPU speed in games.", "System")
            : new CheckResult("Power plan", CheckStatus.Good, name);
    }

    private static CheckResult? Memory()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Speed, ConfiguredClockSpeed, SMBIOSMemoryType, Capacity FROM Win32_PhysicalMemory");
        var sticks = searcher.Get().Cast<ManagementObject>().ToList();
        if (sticks.Count == 0) return null;
        var configured = sticks.Select(s => Convert.ToInt32(s["ConfiguredClockSpeed"] ?? 0)).Min();
        var type = Convert.ToInt32(sticks[0]["SMBIOSMemoryType"] ?? 0);
        var totalGb = sticks.Sum(s => Convert.ToInt64(s["Capacity"] ?? 0L)) / (1024L * 1024 * 1024);
        var isDdr5 = type == 34;
        var jedecOnly = isDdr5 ? configured <= 4800 : configured <= 2666;
        return jedecOnly
            ? new CheckResult("Memory speed", CheckStatus.Warn,
                $"{totalGb} GB at {configured} MT/s, the safe default. If your RAM is rated faster, turn on {(isDdr5 ? "EXPO" : "XMP/DOCP")} in the BIOS.")
            : new CheckResult("Memory speed", CheckStatus.Good, $"{totalGb} GB at {configured} MT/s.");
    }

    private static CheckResult? DedicatedGpu(AeoxContext ctx)
    {
        if (ctx.GameExe is null || !ctx.Hardware.HasMultipleGpus) return null;
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
        var value = key?.GetValue(ctx.GameExe) as string ?? string.Empty;
        return value.Contains("GpuPreference=2;")
            ? new CheckResult("Graphics card", CheckStatus.Good, "The game is set to always use your dedicated GPU.")
            : new CheckResult("Graphics card", CheckStatus.Warn, "Windows may run the game on built-in graphics. Pin it to your dedicated GPU.", "System");
    }

    private static CheckResult Optimizers()
    {
        var found = InstalledPrograms()
            .Where(n => OptimizerNames.Any(o => System.Text.RegularExpressions.Regex.IsMatch(n, $@"\b{System.Text.RegularExpressions.Regex.Escape(o)}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return found.Count == 0
            ? new CheckResult("Optimizer tools", CheckStatus.Good, "None installed.")
            : new CheckResult("Optimizer tools", CheckStatus.Warn,
                $"{string.Join(", ", found)}. These tools often change hidden system settings that cost FPS. Undo their changes and uninstall them.");
    }

    private static CheckResult BackgroundApps()
    {
        var running = Process.GetProcesses().Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var found = HeavyBackgroundApps.Where(a => running.Contains(a.Process)).Select(a => a.Name).Distinct().ToList();
        return found.Count == 0
            ? new CheckResult("Background apps", CheckStatus.Good, "No known FPS-heavy apps running.")
            : new CheckResult("Background apps", CheckStatus.Warn, $"{string.Join(", ", found)} running. Close them while playing, they poll hardware and cost frame time.");
    }

    private static CheckResult? Connection()
    {
        var c = NetworkProbe.DetectConnection();
        if (c is null) return new CheckResult("Connection", CheckStatus.Warn, "No active network connection.", "Network");
        return c.IsWireless
            ? new CheckResult("Connection", CheckStatus.Warn, "Wi-Fi adds jitter and lag spikes. A LAN cable is the biggest network upgrade.", "Network")
            : new CheckResult("Connection", CheckStatus.Good, $"Ethernet, {c.SpeedMbps} Mbps.");
    }

    private static CheckResult MouseAcceleration()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
        var speed = key?.GetValue("MouseSpeed") as string;
        return speed == "0"
            ? new CheckResult("Mouse acceleration", CheckStatus.Good, "Off, so aim is consistent.")
            : new CheckResult("Mouse acceleration", CheckStatus.Warn, "Enhance pointer precision is on, so the same hand movement aims differently.", "System");
    }

    private static CheckResult GameMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
        var on = key?.GetValue("AutoGameModeEnabled") is not int v || v == 1;
        return on
            ? new CheckResult("Game Mode", CheckStatus.Good, "On.")
            : new CheckResult("Game Mode", CheckStatus.Warn, "Off. Windows may run updates and background work during matches.", "System");
    }

    private static CheckResult GpuScheduling()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
        var on = key?.GetValue("HwSchMode") is int v && v == 2;
        return new CheckResult("GPU scheduling", on ? CheckStatus.Good : CheckStatus.Info,
            on ? "Hardware-accelerated GPU scheduling is on." : "Hardware-accelerated GPU scheduling is off. It is required for DLSS Frame Generation; otherwise it rarely matters.");
    }

    private static CheckResult? DiskSpace(AeoxContext ctx)
    {
        var path = ctx.GameExe ?? ctx.Paths.SavedDir;
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root)) return null;
        var free = new DriveInfo(root).AvailableFreeSpace / (1024L * 1024 * 1024);
        return free < 20
            ? new CheckResult("Disk space", CheckStatus.Warn, $"Only {free} GB free on {root}. Low space causes stutter when the game streams assets.")
            : new CheckResult("Disk space", CheckStatus.Good, $"{free} GB free on {root}");
    }

    private static CheckResult MemoryIntegrity()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
        var on = key?.GetValue("Enabled") is int v && v == 1;
        return new CheckResult("Memory Integrity", CheckStatus.Info,
            on ? "On. It protects Windows from malicious drivers and costs a few percent of FPS. Aeox leaves security settings to you."
               : "Off.");
    }

    private static IEnumerable<string> InstalledPrograms()
    {
        string[] paths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var p in paths)
            {
                using var key = hive.OpenSubKey(p);
                if (key is null) continue;
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var app = key.OpenSubKey(sub);
                    if (app?.GetValue("DisplayName") is string name && name.Length > 0) yield return name;
                }
            }
        }
    }

    private static int MaxRefreshAtCurrentResolution()
    {
        var current = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
        if (!EnumDisplaySettings(null, -1, ref current)) return 0;
        var max = current.dmDisplayFrequency;
        var mode = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
        for (var i = 0; EnumDisplaySettings(null, i, ref mode); i++)
        {
            if (mode.dmPelsWidth == current.dmPelsWidth && mode.dmPelsHeight == current.dmPelsHeight && mode.dmDisplayFrequency > max)
                max = mode.dmDisplayFrequency;
        }
        return max;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
