using System.Management;
using Microsoft.Win32;

namespace Aeox.Core.Hardware;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

public sealed record HardwareInfo(string CpuName, IReadOnlyList<string> Gpus, int RefreshRate = 60, int ScreenWidth = 1920, int ScreenHeight = 1080)
{
    public int FrameCapForDisplay
    {
        get
        {
            int[] common = { 60, 75, 100, 120, 144, 165, 170, 180, 240, 280, 360, 500 };
            return common.OrderBy(c => Math.Abs(c - RefreshRate)).First();
        }
    }

    public bool IsX3D => CpuName.Contains("X3D", StringComparison.OrdinalIgnoreCase);

    public bool HasMultipleGpus => Gpus.Count(g => !g.Contains("Basic", StringComparison.OrdinalIgnoreCase) && !g.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) > 1;

    public bool HasNvidia => Gpus.Any(g => g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase));

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
        catch (ManagementException)
        {
        }
        return new HardwareInfo(cpu.Trim(), gpus, refresh, width > 0 ? width : 1920, height > 0 ? height : 1080);
    }
}
