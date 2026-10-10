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
        var root = Path.Combine(Local, "VALORANT", "Saved", "Config");
        if (!Directory.Exists(root)) return null;
        var file = Directory.EnumerateFiles(root, "RiotUserSettings.ini", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (file is null) return null;
        var sens = Find(File.ReadAllText(file), @"EAresFloatSettingName::MouseSensitivity=([\d.]+)");
        return sens is null ? null : new ImportedSens("valorant", sens.Value, null, 103, "valorant settings");
    }

    private static ImportedSens? Cs2()
    {
        var files = OtherGames.SteamLibraries()
            .Select(l => Path.Combine(l, "userdata"))
            .Where(Directory.Exists)
            .SelectMany(u => Directory.EnumerateDirectories(u))
            .Select(u => Path.Combine(u, "730", "local", "cfg", "cs2_user_convars_0_slot0.vcfg"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
        if (files.Count == 0) return null;
        var text = File.ReadAllText(files[0]);
        var sens = Find(text, "\"sensitivity\"\\s+\"([\\d.]+)\"");
        if (sens is null) return null;
        var yaw = Find(text, "\"m_yaw\"\\s+\"([\\d.]+)\"");
        return new ImportedSens("cs2", sens.Value, yaw, 106.26, "cs2 settings, newest steam account");
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
