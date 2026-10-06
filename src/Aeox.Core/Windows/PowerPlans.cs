using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Aeox.Core.Windows;

public static partial class PowerPlans
{
    public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string PowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";
    public const string UltimatePerformanceTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public static string? GetActive()
    {
        var output = Run("/getactivescheme");
        var m = GuidPattern().Match(output);
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    public static void SetActive(string guid)
    {
        Run($"/setactive {guid}");
    }

    public static string Name(string? guid) => guid?.ToLowerInvariant() switch
    {
        Balanced => "Balanced",
        HighPerformance => "High performance",
        PowerSaver => "Power saver",
        UltimatePerformanceTemplate => "Ultimate Performance",
        null => "unknown",
        _ => "custom plan"
    };

    private static string Run(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var text = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return text;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();
}
