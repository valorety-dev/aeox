using System.Diagnostics;

namespace Aeox.Core.Live;

public sealed record RunningApp(string Key, string Name, string? Company, long MemoryBytes, int Count, bool HasWindow, string? Hint);

public static class BackgroundApps
{
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "Aeox", "AeoxDriver", "AeoxScan",
        "steam", "steamwebhelper", "steamservice", "EpicGamesLauncher", "EpicWebHelper", "EpicOnlineServices", "EOSOverlayRenderer-Win64-Shipping",
        "Battle.net", "Agent", "BlizzardError", "RiotClientServices", "RiotClientUx", "RiotClientUxRender", "RiotClientCrashHandler",
        "EADesktop", "EABackgroundService", "EALocalHostSvc", "upc", "UbisoftConnect", "UplayWebCore", "UbisoftGameLauncher", "UbisoftGameLauncher64",
        "GalaxyClient", "GalaxyClientService", "RockstarService", "SocialClubHelper", "LauncherPatcher",
        "EasyAntiCheat", "EasyAntiCheat_EOS", "EasyAntiCheat_Setup", "BEService", "BEService_x64", "vgc", "vgtray", "FACEIT", "faceitservice",
        "dotnet", "node", "python", "pythonw", "py", "java", "javaw", "ruby", "php", "git", "ssh", "wsl", "wslhost", "wslservice", "bash",
        "msedgewebview2", "conhost", "dllhost", "OpenConsole", "WindowsTerminal", "cmd", "powershell", "pwsh"
    };

    private static readonly string[] DriverVendors = { "NVIDIA", "Advanced Micro Devices", "AMD", "Intel", "Realtek" };

    private static readonly string[] DriverFolders = { @"\NVIDIA Corporation\", @"\NVIDIA\", @"\AMD\", @"\ATI Technologies\", @"\Intel\", @"\Realtek\", @"\Microsoft GameInput\" };

    private static readonly string[] DeviceVendors = { "Logitech", "Razer", "SteelSeries", "Corsair", "HyperX", "Roccat", "Glorious", "Wooting" };

    private sealed record Proc(Process Process, string Path, bool Window, long Memory);

    public static IEnumerable<string> GameProcessNames() =>
        Aeox.Core.Game.OtherGames.All.SelectMany(d => d.ExeNames).Select(Path.GetFileNameWithoutExtension).Where(n => n is not null).Select(n => n!)
            .Append(Aeox.Core.Game.GameRunning.GameProcess);

    public static IReadOnlyList<RunningApp> Scan(IEnumerable<string> keep)
    {
        var groups = Collect(keep);
        var result = new List<RunningApp>();
        foreach (var (root, procs) in groups)
        {
            var lead = procs.OrderByDescending(p => p.Window).ThenByDescending(p => p.Memory).First();
            var info = SafeInfo(lead.Path);
            var company = info?.CompanyName?.Trim();
            var name = !string.IsNullOrWhiteSpace(info?.ProductName) && !info!.ProductName!.Contains("Operating System", StringComparison.OrdinalIgnoreCase) ? info.ProductName!.Trim()
                : !string.IsNullOrWhiteSpace(info?.FileDescription) ? info!.FileDescription!.Trim()
                : lead.Process.ProcessName;
            var hint = company is not null && DeviceVendors.Any(v => company.Contains(v, StringComparison.OrdinalIgnoreCase))
                ? "device software, closing it can reset dpi or lighting" : null;
            result.Add(new RunningApp(root, name, company, procs.Sum(p => p.Memory), procs.Count, procs.Any(p => p.Window), hint));
        }
        foreach (var p in groups.SelectMany(g => g.Value)) p.Process.Dispose();
        return result.OrderBy(a => a.HasWindow).ThenByDescending(a => a.MemoryBytes).ToList();
    }

    public static async Task<int> CloseAsync(IEnumerable<string> keys, IEnumerable<string> keep)
    {
        var wanted = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targets = Collect(keep).Where(g => wanted.Contains(g.Key)).SelectMany(g => g.Value).ToList();
        foreach (var p in targets)
        {
            try { p.Process.CloseMainWindow(); } catch (Exception) { }
        }
        await Task.Delay(2500);
        var closed = 0;
        foreach (var p in targets)
        {
            try
            {
                if (!p.Process.HasExited) p.Process.Kill();
                closed++;
            }
            catch (Exception)
            {
            }
            finally
            {
                p.Process.Dispose();
            }
        }
        return closed;
    }

    private static Dictionary<string, List<Proc>> Collect(IEnumerable<string> keep)
    {
        var keepSet = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
        using var me = Process.GetCurrentProcess();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var all = new List<Proc>();
        foreach (var p in Process.GetProcesses())
        {
            if (p.SessionId != me.SessionId || p.Id == me.Id)
            {
                p.Dispose();
                continue;
            }
            string? path = null;
            try { path = p.MainModule?.FileName; } catch (Exception) { }
            if (path is null)
            {
                p.Dispose();
                continue;
            }
            long memory = 0;
            try { memory = p.WorkingSet64; } catch (Exception) { }
            all.Add(new Proc(p, path, p.MainWindowHandle != IntPtr.Zero, memory));
        }

        var blockedRoots = all.Where(p => Protected.Contains(p.Process.ProcessName) || keepSet.Contains(p.Process.ProcessName))
            .Select(p => RootOf(p.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var groups = new Dictionary<string, List<Proc>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in all)
        {
            var root = RootOf(p.Path);
            var skip = blockedRoots.Contains(root)
                       || p.Path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)
                       || DriverFolders.Any(f => p.Path.Contains(f, StringComparison.OrdinalIgnoreCase))
                       || IsDriverVendor(p.Path);
            if (skip)
            {
                p.Process.Dispose();
                continue;
            }
            if (!groups.TryGetValue(root, out var list)) groups[root] = list = new List<Proc>();
            list.Add(p);
        }
        return groups;
    }

    private static string RootOf(string path)
    {
        string[] bases =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        foreach (var b in bases)
        {
            if (string.IsNullOrEmpty(b) || !path.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = path[(b.Length + 1)..].Split('\\');
            if (parts.Length < 2) return Path.GetDirectoryName(path)!;
            var depth = parts[0] is "Desktop" or "Downloads" or "Documents" or "OneDrive" or "Common Files" or "Temp" && parts.Length > 2 ? 2 : 1;
            return Path.Combine(new[] { b }.Concat(parts.Take(depth)).ToArray());
        }
        return Path.GetDirectoryName(path) ?? path;
    }

    private static bool IsDriverVendor(string path)
    {
        var company = SafeInfo(path)?.CompanyName?.Trim();
        return company is not null && DriverVendors.Any(v => company.StartsWith(v, StringComparison.OrdinalIgnoreCase)) &&
               !path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
    }

    private static FileVersionInfo? SafeInfo(string path)
    {
        try { return FileVersionInfo.GetVersionInfo(path); }
        catch (Exception) { return null; }
    }
}
