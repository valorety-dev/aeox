using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Aeox.Core.Windows;

public static class MouseSettings
{
    public const string RegistryPath = @"HKCU\Control Panel\Mouse";
    private const uint SpiSetMouse = 0x0004;
    private const uint SpifUpdateAndBroadcast = 0x01 | 0x02;

    public static void PushRegistryToSession()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse");
        if (key is null) return;
        int Read(string name, int fallback) =>
            int.TryParse(key.GetValue(name) as string, out var v) ? v : fallback;
        var values = new[] { Read("MouseThreshold1", 6), Read("MouseThreshold2", 10), Read("MouseSpeed", 1) };
        SystemParametersInfo(SpiSetMouse, 0, values, SpifUpdateAndBroadcast);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, int[] vparam, uint winIni);
}
