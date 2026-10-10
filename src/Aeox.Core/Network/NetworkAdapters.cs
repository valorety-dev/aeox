using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Aeox.Core.Changes;
using Microsoft.Win32;

namespace Aeox.Core.Network;

public sealed record AdapterParam(string Keyword, IReadOnlyCollection<string> Allowed, string? Default);

public sealed record AdapterInfo(string Name, bool IsWireless, string ClassKey, string DeviceId, IReadOnlyDictionary<string, AdapterParam> Params)
{
    public string RegistryPath => $@"HKLM\{NetworkAdapters.ClassRoot}\{ClassKey}";
}

public static class NetworkAdapters
{
    public const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    public static readonly (string Keyword, string Value)[] PowerSaving =
    {
        ("*EEE", "0"),
        ("EEELinkAdvertisement", "0"),
        ("AdvancedEEE", "0"),
        ("EnableGreenEthernet", "0"),
        ("GigaLite", "0"),
        ("PowerSavingMode", "0"),
        ("ULPMode", "0"),
        ("ReduceSpeedOnPowerDown", "0"),
        ("SavePowerNowEnabled", "0"),
        ("AutoPowerSaveModeEnabled", "0"),
        ("*SelectiveSuspend", "0"),
        ("MIMOPowerSaveMode", "3")
    };

    public static readonly (string Keyword, string Value)[] Batching =
    {
        ("*InterruptModeration", "0"),
        ("*PacketCoalescing", "0")
    };

    public static readonly (string Keyword, string Value)[] Roaming =
    {
        ("RoamAggressiveness", "0"),
        ("RoamIndicateTh", "80"),
        ("RoamingAggressiveness", "0")
    };

    public static AdapterInfo? Active()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .FirstOrDefault(n => n.GetIPProperties().GatewayAddresses
                    .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)));
            if (nic is null) return null;

            using var cls = Registry.LocalMachine.OpenSubKey(ClassRoot);
            if (cls is null) return null;
            foreach (var sub in cls.GetSubKeyNames())
            {
                if (!int.TryParse(sub, out _)) continue;
                using var key = cls.OpenSubKey(sub);
                if (key?.GetValue("NetCfgInstanceId") is not string id || !string.Equals(id, nic.Id, StringComparison.OrdinalIgnoreCase)) continue;
                var device = key.GetValue("DeviceInstanceID") as string;
                if (string.IsNullOrEmpty(device)) return null;
                return new AdapterInfo(nic.Description, nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211, sub, device, ReadParams(key));
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        return null;
    }

    public static IReadOnlyList<Change> Pick(AdapterInfo adapter, IEnumerable<(string Keyword, string Value)> wanted)
    {
        var list = new List<Change>();
        foreach (var (keyword, value) in wanted)
        {
            if (!adapter.Params.TryGetValue(keyword, out var p) || !p.Allowed.Contains(value)) continue;
            list.Add(Change.Adapter(adapter.RegistryPath, adapter.DeviceId, keyword, "sz:" + value, p.Default is null ? null : "sz:" + p.Default));
        }
        return list;
    }

    public static Change AllowPowerOff(AdapterInfo adapter, bool allow) =>
        Change.Adapter(adapter.RegistryPath, adapter.DeviceId, "PnPCapabilities", allow ? null : "dword:24");

    private static IReadOnlyDictionary<string, AdapterParam> ReadParams(RegistryKey adapterKey)
    {
        var result = new Dictionary<string, AdapterParam>(StringComparer.OrdinalIgnoreCase);
        using var parms = adapterKey.OpenSubKey(@"Ndi\params");
        if (parms is null) return result;
        foreach (var name in parms.GetSubKeyNames())
        {
            using var p = parms.OpenSubKey(name);
            using var en = p?.OpenSubKey("enum");
            if (p is null || en is null) continue;
            var allowed = en.GetValueNames().Where(v => v.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            result[name] = new AdapterParam(name, allowed, p.GetValue("default")?.ToString());
        }
        return result;
    }
}
