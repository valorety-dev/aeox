using System.Globalization;
using System.Text.RegularExpressions;
using Aeox.Core.Game;

namespace Aeox.Core.Aim;

public sealed record ImportedSens(string Game, double Sens, double? Yaw, double? HFov, string Source);

public static partial class AimImport
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static IReadOnlyList<ImportedSens> FindAll()
    {
        var list = new List<ImportedSens>();
        void Try(Func<ImportedSens?> f)
        {
            try
            {
                if (f() is { } r) list.Add(r);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
            }
        }
        Try(Valorant);
        Try(Cs2);
        Try(Apex);
        Try(Cod);
        return list;
    }

    private static ImportedSens? Valorant()
    {
        var active = ActiveValorantAccount();
        if (active is null) return null;
        var file = Directory.EnumerateDirectories(Path.Combine(Local, "VALORANT", "Saved", "Config"))
            .Where(d => Path.GetFileName(d).StartsWith(active, StringComparison.OrdinalIgnoreCase))
            .Select(d => Path.Combine(d, "Windows", "RiotUserSettings.ini"))
            .FirstOrDefault(File.Exists);
        if (file is null) return null;
        var sens = Find(File.ReadAllText(file), @"EAresFloatSettingName::MouseSensitivity=([\d.]+)");
        return sens is null ? null : new ImportedSens("valorant", Math.Round(sens.Value, 4), null, 103, "valorant account you played last");
    }

    private static string? ActiveValorantAccount()
    {
        var logs = Path.Combine(Local, "VALORANT", "Saved", "Logs");
        var config = Path.Combine(Local, "VALORANT", "Saved", "Config");
        if (!Directory.Exists(logs) || !Directory.Exists(config)) return null;
        var ids = Directory.EnumerateDirectories(config)
            .Select(Path.GetFileName)
            .Select(d => Regex.Match(d ?? "", @"^([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})", RegexOptions.IgnoreCase))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
        if (ids.Count == 0) return null;
        foreach (var log in Directory.EnumerateFiles(logs, "ShooterGame*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
        {
            string text;
            try
            {
                using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text = reader.ReadToEnd();
            }
            catch (IOException)
            {
                continue;
            }
            var best = ids.Select(id => (id, last: text.LastIndexOf(id, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x.last >= 0).OrderByDescending(x => x.last).FirstOrDefault();
            if (best.id is not null) return best.id;
        }
        return null;
    }

    private static ImportedSens? Cs2()
    {
        var libs = OtherGames.SteamLibraries();
        if (libs.Count == 0) return null;
        var login = Path.Combine(libs[0], "config", "loginusers.vdf");
        if (!File.Exists(login)) return null;
        var users = new List<(long id, long stamp)>();
        long current = 0;
        foreach (var line in File.ReadLines(login))
        {
            var t = line.Trim();
            var id = Regex.Match(t, "^\"(7656\\d{13})\"$");
            if (id.Success) { current = long.Parse(id.Groups[1].Value, CultureInfo.InvariantCulture); continue; }
            var stamp = Regex.Match(t, "^\"Timestamp\"\\s+\"(\\d+)\"");
            if (stamp.Success && current != 0) users.Add((current, long.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture)));
        }
        foreach (var (id, _) in users.OrderByDescending(u => u.stamp))
        {
            var account = (id - 76561197960265728L).ToString(CultureInfo.InvariantCulture);
            var file = libs.Select(l => Path.Combine(l, "userdata", account, "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg")).FirstOrDefault(File.Exists);
            if (file is null) continue;
            var text = File.ReadAllText(file);
            var sens = Find(text, "\"sensitivity\"\\s+\"([\\d.]+)\"");
            if (sens is null) continue;
            var yaw = Find(text, "\"m_yaw\"\\s+\"([\\d.]+)\"");
            return new ImportedSens("cs2", sens.Value, yaw, 106.26, "steam account you logged in with last");
        }
        return null;
    }
    private static ImportedSens? Apex()
    {
        var file = Path.Combine(Home, "Saved Games", "Respawn", "Apex", "local", "settings.cfg");
        if (!File.Exists(file)) return null;
        var sens = Find(File.ReadAllText(file), "mouse_sensitivity\\s+\"([\\d.]+)\"");
        return sens is null ? null : new ImportedSens("apex", sens.Value, null, 106.26, "apex settings");
    }

    private static ImportedSens? Cod()
    {
        var root = Path.Combine(Docs, "Call of Duty", "players");
        if (!Directory.Exists(root)) return null;
        var file = Directory.EnumerateFiles(root, "gamerprofile.*.cst", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cst", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (file is null) return null;
        var text = File.ReadAllText(file);
        var sens = Find(text, @"^MouseHorizontalSensibility@\d+\s*=\s*([\d.]+)");
        if (sens is null) return null;
        var fov = Find(text, @"^Fov@\d+\s*=\s*([\d.]+)");
        return new ImportedSens("cod", sens.Value, null, fov, "call of duty settings");
    }

    private static double? Find(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.Multiline);
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
