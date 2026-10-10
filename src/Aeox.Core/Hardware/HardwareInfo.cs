using System.Management;
using System.Text.RegularExpressions;
using Aeox.Core.Network;
using Microsoft.Win32;

namespace Aeox.Core.Hardware;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

public enum GpuTier
{
    Entry,
    Mid,
    Fast
}

public sealed record HardwareInfo(string CpuName, IReadOnlyList<string> Gpus, int RefreshRate = 60, int ScreenWidth = 1920, int ScreenHeight = 1080)
{
    public int Cores { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);
    public int Threads { get; init; } = Environment.ProcessorCount;
    public long RamGb { get; init; }
    public double VramGb { get; init; }
    public bool IsLaptop { get; init; }
    public AdapterInfo? Adapter { get; init; }

    public int FrameCapForDisplay
    {
        get
        {
            int[] common = { 60, 75, 100, 120, 144, 165, 170, 180, 240, 280, 360, 500 };
            return common.OrderBy(c => Math.Abs(c - RefreshRate)).First();
        }
    }

    public bool IsX3D => CpuName.Contains("X3D", StringComparison.OrdinalIgnoreCase);

    public bool IsDualCcdX3D => IsX3D && Threads > 16;

    public bool HasMultipleGpus => Gpus.Count(g => !g.Contains("Basic", StringComparison.OrdinalIgnoreCase) && !g.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) > 1;

    public bool HasNvidia => Gpus.Any(g => g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));

    public string? DedicatedGpu => Gpus.FirstOrDefault(IsDedicated);

    public string PrimaryGpu => DedicatedGpu ?? Gpus.FirstOrDefault() ?? "Unknown GPU";

    public bool IntegratedOnly => DedicatedGpu is null;

    public GpuTier Tier
    {
        get
        {
            if (IntegratedOnly) return GpuTier.Entry;
            if (VramGb <= 0) return GpuTier.Mid;
            if (VramGb < 4.5) return GpuTier.Entry;
            return VramGb >= 10 ? GpuTier.Fast : GpuTier.Mid;
        }
    }

    public bool IsWireless => Adapter?.IsWireless ?? false;

    public GpuVendor PrimaryGpuVendor
    {
        get
        {
            if (HasNvidia) return GpuVendor.Nvidia;
            if (Gpus.Any(g => g.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase))) return GpuVendor.Amd;
            if (Gpus.Any(g => g.Contains("Arc", StringComparison.OrdinalIgnoreCase))) return GpuVendor.Intel;
            if (Gpus.Any(g => g.Contains("AMD", StringComparison.OrdinalIgnoreCase) || g.Contains("Radeon", StringComparison.OrdinalIgnoreCase))) return GpuVendor.Amd;
            return Gpus.Any(g => g.Contains("Intel", StringComparison.OrdinalIgnoreCase)) ? GpuVendor.Intel : GpuVendor.Unknown;
        }
    }

    private static bool IsDedicated(string gpu) =>
        gpu.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
        || Regex.IsMatch(gpu, @"Radeon\s*(\(TM\)\s*)?(RX|Pro|VII)", RegexOptions.IgnoreCase)
        || Regex.IsMatch(gpu, @"Arc\s*(\(TM\)\s*)?[AB]\d{3}", RegexOptions.IgnoreCase);

    public static HardwareInfo Detect()
    {
        var cpu = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string ?? "Unknown CPU";
        var gpus = new List<string>();
        var refresh = 60;
        var width = 0;
        var height = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, CurrentRefreshRate, CurrentHorizontalResolution, CurrentVerticalResolution FROM Win32_VideoController");
            foreach (var o in searcher.Get())
            {
                if (o["Name"] is string name && name.Length > 0) gpus.Add(name.Trim());
                if (o["CurrentRefreshRate"] is uint hz && hz > refresh) refresh = (int)hz;
                if (o["CurrentHorizontalResolution"] is uint w && w > width && o["CurrentVerticalResolution"] is uint h)
                {
                    width = (int)w;
                    height = (int)h;
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
        }
        var info = new HardwareInfo(cpu.Trim(), gpus, refresh, width > 0 ? width : 1920, height > 0 ? height : 1080);
        var (cores, threads) = CpuCounts();
        return info with
        {
            Cores = cores,
            Threads = threads,
            RamGb = RamGigabytes(),
            VramGb = VideoMemoryGb(info.PrimaryGpu),
            IsLaptop = HasBattery(),
            Adapter = NetworkAdapters.Active()
        };
    }

    public static double VideoMemoryGb(string gpuName)
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

    private static (int Cores, int Threads) CpuCounts()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            var cores = 0;
            var threads = 0;
            foreach (var o in s.Get())
            {
                cores += Convert.ToInt32(o["NumberOfCores"] ?? 0);
                threads += Convert.ToInt32(o["NumberOfLogicalProcessors"] ?? 0);
            }
            if (cores > 0 && threads > 0) return (cores, threads);
        }
        catch (Exception)
        {
        }
        return (Math.Max(1, Environment.ProcessorCount / 2), Environment.ProcessorCount);
    }

    private static long RamGigabytes()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var o in s.Get()) return (long)Math.Round(Convert.ToInt64(o["TotalPhysicalMemory"] ?? 0L) / 1024.0 / 1024 / 1024);
        }
        catch (Exception)
        {
        }
        return 0;
    }

    private static bool HasBattery()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_Battery");
            return s.Get().Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
