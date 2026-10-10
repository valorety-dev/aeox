using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aeox.Core.Game;

public sealed record KnownGame(string Exe, string? SavedDir);

public static partial class GameCatalog
{
    private const string ExeName = GameRunning.GameProcess + ".exe";

    private static readonly string[] ProjectNames =
    {
        "Retrac", "Phantom", "Echo", "Nova", "Eon", "Reload", "Lawin", "Galaxy", "FortMP", "Rift", "Solaris", "Aurora",
        "Carbon", "Kyber", "Crystal", "Neonite", "Vortex", "Horizon", "Erbium", "Cobalt", "Momentum", "Polaris", "Arcane"
    };

    private static readonly HashSet<string> GenericFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Users", "AppData", "Local", "Roaming", "Program Files", "Program Files (x86)", "Desktop", "Downloads", "Documents",
        "Games", "Game", "Builds", "Build", "Versions", "Version", "Fortnite", "FortniteGame", "Binaries", "Win64", "Launcher",
        "Launchers", "Epic Games", "OG", "OGFN", "Fortnite Builds", "Program", "Programme", "Programs"
    };

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "$Recycle.Bin", "System Volume Information", "ProgramData", "Recovery", "PerfLogs", "Packages", "Microsoft",
        "Temp", "Google", "Mozilla", "NVIDIA", "NVIDIA Corporation", "D3DSCache", "CrashDumps", "Discord", "node_modules",
        "WindowsApps", "Steam", "steamapps", "Riot Games", "Common Files", "dotnet", "Docker"
    };

    private static readonly ConcurrentDictionary<string, string?> VersionCache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<GameProfile> Discover(IEnumerable<KnownGame> known)
    {
        var retrac = GameProfile.Retrac();
        var live = GameProfile.Fortnite();
        var list = new List<GameProfile> { retrac, live };
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (retrac.FindExe() is { } r) taken.Add(Normalize(r));
        if (live.FindExe() is { } l) taken.Add(Normalize(l));

        foreach (var game in known)
        {
            if (!File.Exists(game.Exe) || !IsGameBuild(game.Exe)) continue;
            var exe = Normalize(game.Exe);
            if (!taken.Add(exe)) continue;
            if (exe.Contains(@"\launcher.retrac.site\", StringComparison.OrdinalIgnoreCase)) continue;
            var name = ProjectName(exe);
            var saved = game.SavedDir is { } s && Directory.Exists(s) ? s : GuessSavedDir(name);
            list.Add(new GameProfile("og-" + Hash(exe), name, ReadVersion(exe), false, new GamePaths(saved), () => File.Exists(exe) ? exe : null));
        }
        var exes = OtherGames.ConfigStoreExes().Concat(known.Select(k => k.Exe)).ToList();
        foreach (var def in OtherGames.All) list.Add(OtherGames.Profile(def, exes));
        return list;
    }

    public static IReadOnlyList<string> ScanForExes(CancellationToken token = default)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<(string Path, int Depth)>
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 6),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 6),
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 6),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 6),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), 6)
        };
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            roots.Add((drive.RootDirectory.FullName, 6));

        foreach (var (root, depth) in roots)
        {
            if (token.IsCancellationRequested) break;
            if (Directory.Exists(root)) Walk(root, depth, found, token);
        }
        return found.ToList();
    }

    public static IReadOnlyList<string> FindExesIn(string folder) =>
        Directory.Exists(folder) ? WalkCollect(folder, 7) : Array.Empty<string>();

    public static string? RunningExe()
    {
        foreach (var p in Process.GetProcessesByName(GameRunning.GameProcess))
        {
            try
            {
                if (p.MainModule?.FileName is { } file && IsGameBuild(file)) return Normalize(file);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
            }
            finally
            {
                p.Dispose();
            }
        }
        return null;
    }

    public static IReadOnlyList<string> SavedDirs()
    {
        var root = GameProfile.LocalAppData;
        var result = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var saved = Path.Combine(dir, "Saved");
                var gus = Path.Combine(saved, "Config", "WindowsClient", "GameUserSettings.ini");
                if (!File.Exists(gus)) continue;
                try
                {
                    if (File.ReadAllText(gus).Contains(RetracGame.UserSettingsSection, StringComparison.OrdinalIgnoreCase)) result.Add(saved);
                }
                catch (IOException)
                {
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        return result;
    }

    public static string? SavedDirWithFreshLog(TimeSpan within) =>
        SavedDirs()
            .Select(d => (Dir: d, Log: new GamePaths(d).GameLog))
            .Where(x => File.Exists(x.Log) && DateTime.UtcNow - File.GetLastWriteTimeUtc(x.Log) < within)
            .OrderByDescending(x => File.GetLastWriteTimeUtc(x.Log))
            .Select(x => x.Dir)
            .FirstOrDefault();

    public static string HistoryKey(string savedDir)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(savedDir.TrimEnd('\\', '/')) ?? savedDir) ?? "game";
        if (folder.Equals("RetracGame", StringComparison.OrdinalIgnoreCase)) return "retrac";
        if (folder.Equals("FortniteGame", StringComparison.OrdinalIgnoreCase)) return "fortnite";
        return Regex.Replace(folder.ToLowerInvariant(), "[^a-z0-9]+", "-");
    }

    public static string? ReadVersion(string exe) => VersionCache.GetOrAdd(Normalize(exe), path =>
    {
        var fromPath = VersionPattern().Match(path);
        if (fromPath.Success) return Trim(fromPath.Groups[1].Value);
        try
        {
            var bytes = File.ReadAllBytes(path);
            var marker = Encoding.ASCII.GetBytes("++Fortnite+Release-");
            var at = bytes.AsSpan().IndexOf(marker);
            if (at < 0)
            {
                marker = Encoding.Unicode.GetBytes("++Fortnite+Release-");
                at = bytes.AsSpan().IndexOf(marker);
                if (at < 0) return null;
                var text = Encoding.Unicode.GetString(bytes, at, Math.Min(80, bytes.Length - at));
                var m = VersionPattern().Match(text);
                return m.Success ? Trim(m.Groups[1].Value) : null;
            }
            var ascii = Encoding.ASCII.GetString(bytes, at, Math.Min(40, bytes.Length - at));
            var match = VersionPattern().Match(ascii);
            return match.Success ? Trim(match.Groups[1].Value) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            return null;
        }
    });

    public static string ProjectName(string exe)
    {
        var parts = Normalize(exe).Split('\\', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var hit = ProjectNames.FirstOrDefault(n => part.Contains(n, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit;
        }
        foreach (var part in parts.Reverse().Skip(1))
        {
            if (part.Length < 3 || part.EndsWith(':') || GenericFolders.Contains(part)) continue;
            if (part.Contains("Fortnite", StringComparison.OrdinalIgnoreCase) || VersionPattern().IsMatch(part) || Regex.IsMatch(part, @"^\d+(\.\d+)*$")) continue;
            if (part.StartsWith('.') || part.Contains(".site", StringComparison.OrdinalIgnoreCase)) continue;
            return char.ToUpperInvariant(part[0]) + part[1..];
        }
        return "OG Fortnite";
    }

    public static string? SeasonFor(double? version)
    {
        if (version is not { } v) return null;
        var major = (int)Math.Floor(v);
        return major switch
        {
            <= 0 => null,
            1 => "Chapter 1",
            >= 2 and <= 9 => $"Chapter 1 Season {major}",
            10 => "Chapter 1 Season X",
            >= 11 and <= 18 => $"Chapter 2 Season {major - 10}",
            >= 19 and <= 22 => $"Chapter 3 Season {major - 18}",
            >= 23 and <= 26 => $"Chapter 4 Season {major - 22}",
            27 => "Chapter 4 Season OG",
            >= 28 and <= 31 => $"Chapter 5 Season {major - 27}",
            _ => null
        };
    }

    public static bool IsGameBuild(string exe) =>
        Normalize(exe).EndsWith(@"\FortniteGame\Binaries\Win64\" + ExeName, StringComparison.OrdinalIgnoreCase);

    private static string GuessSavedDir(string projectName)
    {
        var own = Path.Combine(GameProfile.LocalAppData, projectName + "Game", "Saved");
        return Directory.Exists(own) ? own : Path.Combine(GameProfile.LocalAppData, "FortniteGame", "Saved");
    }

    private static void Walk(string dir, int depth, HashSet<string> found, CancellationToken token)
    {
        foreach (var exe in WalkCollect(dir, depth, token)) found.Add(exe);
    }

    private static List<string> WalkCollect(string root, int maxDepth, CancellationToken token = default)
    {
        var result = new List<string>();
        var stack = new Stack<(string Dir, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0 && !token.IsCancellationRequested)
        {
            var (dir, depth) = stack.Pop();
            try
            {
                var exe = Path.Combine(dir, ExeName);
                if (File.Exists(exe) && IsGameBuild(exe))
                {
                    result.Add(Normalize(exe));
                    continue;
                }
                if (depth >= maxDepth) continue;
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (SkipDirs.Contains(name) || name.StartsWith('.')) continue;
                    if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue;
                    stack.Push((sub, depth + 1));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
            }
        }
        return result;
    }

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception) { return path; }
    }

    private static string Hash(string exe) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(exe.ToLowerInvariant())))[..10].ToLowerInvariant();

    private static string Trim(string version) => version.TrimEnd('.');

    [GeneratedRegex(@"Release-(\d{1,2}\.\d{1,2})")]
    private static partial Regex VersionPattern();
}
