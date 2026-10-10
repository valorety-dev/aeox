using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Aeox.Core.Network;

namespace Aeox.Core.Changes;

public sealed class ElevationDeclinedException : Exception
{
    public ElevationDeclinedException()
        : base("Windows asked for admin rights and it was declined. Nothing was changed.")
    {
    }
}

public static class Elevation
{
    public const string Arg = "--apply-elevated";

    private static readonly (string Path, string Value)[] AllowedMachineValues =
    {
        (@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex"),
        (@"HKLM\SYSTEM\CurrentControlSet\Services\amd3dvcache\Preferences", "DefaultType")
    };

    private sealed record Step(ChangeKind Kind, string Target, string Section, string Key, string? Value);

    public static bool IsAdmin
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static void Run(IReadOnlyList<PlannedChange> plan)
    {
        if (plan.Count == 0) return;
        if (Environment.ProcessPath is not { } exe) throw new ElevationDeclinedException();
        var file = Path.Combine(Path.GetTempPath(), $"aeox-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(plan.Select(p => new Step(p.Change.Kind, p.Change.Target, p.Change.Section, p.Change.Key, p.NewValue)).ToList()));
        try
        {
            using var process = Process.Start(new ProcessStartInfo(exe, $"{Arg} \"{file}\"") { UseShellExecute = true, Verb = "runas" });
            if (process is null) throw new ElevationDeclinedException();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("The admin step could not write all values. Nothing else was changed.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new ElevationDeclinedException();
        }
        finally
        {
            try { File.Delete(file); } catch (IOException) { }
        }
    }

    public static int RunFromFile(string file)
    {
        try
        {
            var steps = JsonSerializer.Deserialize<List<Step>>(File.ReadAllText(file)) ?? new List<Step>();
            if (steps.Count == 0 || !steps.All(IsAllowed)) return 2;
            var plan = steps.Select(s => new PlannedChange(new Change(s.Kind, s.Target, s.Section, s.Key, s.Value), null, s.Value, "admin")).ToList();
            ChangeEngine.Write(plan);
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static bool IsAllowed(Step s) => s.Kind switch
    {
        ChangeKind.Adapter => s.Target.StartsWith($@"HKLM\{NetworkAdapters.ClassRoot}\", StringComparison.OrdinalIgnoreCase)
                              && !s.Target[($@"HKLM\{NetworkAdapters.ClassRoot}\".Length)..].Contains('\\')
                              && !s.Key.Contains('\\'),
        ChangeKind.Registry => AllowedMachineValues.Any(a => string.Equals(a.Path, s.Target, StringComparison.OrdinalIgnoreCase)
                                                             && string.Equals(a.Value, s.Key, StringComparison.OrdinalIgnoreCase)),
        _ => false
    };
}
