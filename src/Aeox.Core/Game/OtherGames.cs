using Aeox.Core.Changes;
using Aeox.Core.Ini;
using Aeox.Core.Tweaks;
using Microsoft.Win32;

namespace Aeox.Core.Game;

public sealed record GameDef(
    string Key,
    string Name,
    string[] ExeNames,
    string[] SteamPaths,
    Func<IReadOnlyList<string>> FindSettings,
    Func<AeoxContext, IReadOnlyList<Tweak>>? Tweaks,
    string? Note = null,
    Func<AeoxContext, IReadOnlyList<ChoiceSetting>>? Choices = null);

public static class OtherGames
{
    private const string Sg = "ScalabilityGroups";
    private const string Embark = "/Script/EmbarkUserSettings.EmbarkGameUserSettings";
    private const string Marvel = "/Script/Marvel.MarvelGameUserSettings";
    private const string Tsl = "/Script/TslGame.TslGameUserSettings";
    private const string Shooter = "/Script/ShooterGame.ShooterGameUserSettings";
    private const string Riot = "Settings";

    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static IReadOnlyList<GameDef> All { get; } = new List<GameDef>
    {
        new("valorant", "Valorant", new[] { "VALORANT-Win64-Shipping.exe" }, Array.Empty<string>(),
            () => Glob(Path.Combine(Local, "VALORANT", "Saved", "Config"), "GameUserSettings.ini", "RiotUserSettings.ini"),
            Valorant, "Valorant also keeps settings on your Riot account. Change them while the game is closed.", ValorantChoices),
        new("cs2", "Counter-Strike 2", new[] { "cs2.exe" }, new[] { @"Counter-Strike Global Offensive\game\bin\win64\cs2.exe" },
            () => SteamUserFiles(@"730\local\cfg\cs2_video.txt"), Cs2, null, Cs2Choices),
        new("apex", "Apex Legends", new[] { "r5apex.exe", "r5apex_dx12.exe" }, new[] { @"Apex Legends\r5apex.exe" },
            () => Existing(Path.Combine(Home, "Saved Games", "Respawn", "Apex", "local", "videoconfig.txt")), Apex, null, ApexChoices),
        new("cod", "Call of Duty", new[] { "cod.exe", "cod24-cod.exe", "cod25-cod.exe" }, new[] { @"Call of Duty HQ\cod.exe" },
            () => Directory.Exists(Path.Combine(Docs, "Call of Duty", "players"))
                ? Directory.EnumerateFiles(Path.Combine(Docs, "Call of Duty", "players"), "options.4.cod*.cst").ToList()
                : Array.Empty<string>(), Cod, null, CodChoices),
        new("bf6", "Battlefield 6", new[] { "bf6.exe" }, Array.Empty<string>(),
            () => Existing(Path.Combine(Docs, "Battlefield 6", "settings", "PROFSAVE_profile")), Bf6),
        new("overwatch", "Overwatch 2", new[] { "Overwatch.exe" }, Array.Empty<string>(),
            () => Existing(Path.Combine(Docs, "Overwatch", "Settings", "Settings_v0.ini")), Overwatch, null, OverwatchChoices),
        new("gta5", "GTA V", new[] { "GTA5.exe", "GTA5_Enhanced.exe" }, new[] { @"Grand Theft Auto V\GTA5.exe", @"Grand Theft Auto V Enhanced\GTA5_Enhanced.exe" },
            () => Existing(Path.Combine(Docs, "Rockstar Games", "GTA V", "settings.xml"), Path.Combine(Docs, "Rockstar Games", "GTAV Enhanced", "settings.xml")), Gta, null, GtaChoices),
        new("destiny2", "Destiny 2", new[] { "destiny2.exe" }, new[] { @"Destiny 2\destiny2.exe" },
            () => Existing(Path.Combine(Roaming, "Bungie", "DestinyPC", "prefs", "cvars.xml")), Destiny, null, DestinyChoices),
        new("minecraft", "Minecraft", new[] { "javaw.exe" }, Array.Empty<string>(),
            () => Existing(Path.Combine(Roaming, ".minecraft", "options.txt")), Minecraft, null, MinecraftChoices),
        new("thefinals", "The Finals", new[] { "Discovery.exe" }, new[] { @"The Finals\Discovery\Binaries\Win64\Discovery.exe" },
            () => Existing(Path.Combine(Local, "Discovery", "Saved", "Config", "WindowsClient", "GameUserSettings.ini")),
            ctx => EmbarkGame(ctx, "MotionBlurEnabled", "False"), null, UeChoices(Embark)),
        new("arcraiders", "Arc Raiders", new[] { "PioneerGame.exe" }, new[] { @"Arc Raiders\PioneerGame.exe" },
            () => Existing(Path.Combine(Local, "PioneerGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini")),
            ctx => EmbarkGame(ctx, "MotionBlurMode", "Off"), null, UeChoices(Embark)),
        new("marvelrivals", "Marvel Rivals", new[] { "Marvel-Win64-Shipping.exe" }, new[] { @"MarvelRivals\MarvelGame\Marvel\Binaries\Win64\Marvel-Win64-Shipping.exe" },
            () => Existing(Path.Combine(Local, "Marvel", "Saved", "Config", "Windows", "GameUserSettings.ini")), MarvelRivals, null, UeChoices(Marvel)),
        new("pubg", "PUBG", new[] { "TslGame.exe" }, new[] { @"PUBG\TslGame\Binaries\Win64\TslGame.exe" },
            () => Existing(Path.Combine(Local, "TslGame", "Saved", "Config", "WindowsNoEditor", "GameUserSettings.ini")), Pubg, null, PubgChoices),
        new("r6", "Rainbow Six Siege", new[] { "RainbowSix.exe", "RainbowSix_Vulkan.exe" }, new[] { @"Tom Clancy's Rainbow Six Siege\RainbowSix.exe" },
            () => Glob(Path.Combine(Docs, "My Games", "Rainbow Six - Siege"), "GameSettings.ini"), Siege),
        new("rocketleague", "Rocket League", new[] { "RocketLeague.exe" }, new[] { @"rocketleague\Binaries\Win64\RocketLeague.exe" },
            () => Existing(Path.Combine(Docs, "My Games", "Rocket League", "TAGame", "Config", "TASystemSettings.ini")), RocketLeague),
        new("deltaforce", "Delta Force", new[] { "DeltaForceClient-Win64-Shipping.exe" }, new[] { @"Delta Force\Game\DeltaForce\Binaries\Win64\DeltaForceClient-Win64-Shipping.exe" },
            NoFiles, null),
        new("arenabreakout", "Arena Breakout: Infinite", new[] { "UAGame.exe" }, new[] { @"ABInfinite\ABInfinite\Binaries\Win64\UAGame.exe" },
            NoFiles, null),
        new("rust", "Rust", new[] { "RustClient.exe" }, new[] { @"Rust\RustClient.exe" }, NoFiles, null),
        new("warthunder", "War Thunder", new[] { "aces.exe" }, new[] { @"War Thunder\win64\aces.exe" }, NoFiles, null),
        new("league", "League of Legends", new[] { "League of Legends.exe" }, Array.Empty<string>(),
            () => Existing(@"C:\Riot Games\League of Legends\Config\game.cfg"), League,
            "League also syncs settings from your account. If one jumps back, set it once in game.")
    };

    public static GameProfile Profile(GameDef def, IReadOnlyList<string> knownExes)
    {
        var files = SafeFiles(def);
        var exe = FindExe(def, knownExes);
        var dir = files.Count > 0 ? Path.GetDirectoryName(files[0])! : Local;
        return new GameProfile("game-" + def.Key, def.Name, null, false, new GamePaths(dir), () => exe is not null && File.Exists(exe) ? exe : null)
        {
            IsFortnite = false,
            SettingsFiles = files,
            Tweaks = def.Tweaks,
            Choices = def.Choices,
            Note = def.Note
        };
    }

    public static IReadOnlyList<string> ConfigStoreExes()
    {
        var list = new List<string>();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children");
            if (root is null) return list;
            foreach (var name in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(name);
                if (key?.GetValue("MatchedExeFullPath") is string exe && File.Exists(exe)) list.Add(exe);
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        return list;
    }

    private static IReadOnlyList<string> SafeFiles(GameDef def)
    {
        try { return def.FindSettings().Where(File.Exists).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static string? FindExe(GameDef def, IReadOnlyList<string> knownExes)
    {
        foreach (var exe in knownExes)
        {
            var file = Path.GetFileName(exe);
            if (!def.ExeNames.Any(n => string.Equals(n, file, StringComparison.OrdinalIgnoreCase))) continue;
            if (def.Key == "minecraft" && !exe.Contains("minecraft", StringComparison.OrdinalIgnoreCase) && !exe.Contains("4297127D64EC6", StringComparison.OrdinalIgnoreCase)) continue;
            return exe;
        }
        foreach (var lib in SteamLibraries())
        {
            foreach (var rel in def.SteamPaths)
            {
                var path = Path.Combine(lib, "steamapps", "common", rel);
                if (File.Exists(path)) return path;
            }
        }
        if (def.Key == "valorant")
        {
            var riot = @"C:\Riot Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe";
            if (File.Exists(riot)) return riot;
        }
        if (def.Key == "league")
        {
            var lol = @"C:\Riot Games\League of Legends\Game\League of Legends.exe";
            if (File.Exists(lol)) return lol;
        }
        return null;
    }

    public static IReadOnlyList<string> SteamLibraries()
    {
        var result = new List<string>();
        try
        {
            if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is not string steam) return result;
            steam = steam.Replace('/', '\\');
            result.Add(steam);
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) return result;
            foreach (var line in File.ReadLines(vdf))
            {
                var t = line.Trim();
                if (!t.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = t.Split('"');
                if (parts.Length >= 4) result.Add(parts[3].Replace(@"\\", @"\"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> NoFiles() => Array.Empty<string>();

    private static IReadOnlyList<string> Existing(params string[] paths) => paths.Where(File.Exists).ToList();

    private static IReadOnlyList<string> Glob(string root, params string[] names)
    {
        if (!Directory.Exists(root)) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var name in names)
            list.AddRange(Directory.EnumerateFiles(root, name, new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, IgnoreInaccessible = true }));
        return list;
    }

    private static IReadOnlyList<string> SteamUserFiles(string relative)
    {
        var list = new List<string>();
        foreach (var lib in SteamLibraries())
        {
            var userdata = Path.Combine(lib, "userdata");
            if (!Directory.Exists(userdata)) continue;
            foreach (var user in Directory.EnumerateDirectories(userdata))
            {
                var file = Path.Combine(user, relative);
                if (File.Exists(file)) list.Add(file);
            }
        }
        return list;
    }

    private static IEnumerable<string> Files(AeoxContext ctx, string fileName) =>
        ctx.Game.SettingsFiles.Where(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> FilesLike(AeoxContext ctx, string prefix) =>
        ctx.Game.SettingsFiles.Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static Tweak Make(string id, TweakCategory category, string title, string description, Func<AeoxContext, IReadOnlyList<Change>> changes,
        Func<AeoxContext, bool>? recommended = null, Func<AeoxContext, string?>? tag = null, Func<AeoxContext, bool>? extra = null) =>
        new(id, category, title, description, string.Empty, changes,
            ctx => (extra?.Invoke(ctx) ?? true) && changes(ctx).Count > 0, recommended, tag);

    private static Func<AeoxContext, bool> Nvidia => ctx => ctx.Hardware.HasNvidia;
    private static Func<AeoxContext, string?> NvidiaTag => _ => "reflex · nvidia";

    private static IReadOnlyList<Change> Ini(IEnumerable<string> files, string section, params (string Key, string Value)[] values) =>
        files.SelectMany(f => values.Select(v => Change.Ini(f, section, v.Key, v.Value))).ToList();

    private static IReadOnlyList<Change> Text(IEnumerable<string> files, string format, params (string Key, string Value, string Default)[] values) =>
        files.Where(f => values.Any(v => TextSettings.Get(f, format, v.Key) is not null))
            .SelectMany(f => values.Where(v => TextSettings.Get(f, format, v.Key) is not null).Select(v => Change.Text(f, format, v.Key, v.Value, v.Default)))
            .ToList();

    private static readonly object DocLock = new();
    private static readonly Dictionary<string, (DateTime Stamp, IniDocument Doc)> Docs2 = new(StringComparer.OrdinalIgnoreCase);

    private static IniDocument? Doc(string file)
    {
        try
        {
            var stamp = File.GetLastWriteTimeUtc(file);
            lock (DocLock)
            {
                if (Docs2.TryGetValue(file, out var hit) && hit.Stamp == stamp) return hit.Doc;
                var doc = IniDocument.Load(file);
                Docs2[file] = (stamp, doc);
                return doc;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<Change> IniHas(IEnumerable<string> files, string section, params (string Key, string Value)[] values) =>
        files.SelectMany(f =>
        {
            var doc = Doc(f);
            return values.Where(v => doc?.Get(section, v.Key) is not null).Select(v => Change.Ini(f, section, v.Key, v.Value));
        }).ToList();

    private static IReadOnlyList<Change> Group(IEnumerable<string> files, string group, string value) =>
        files.Select(f => Change.Ini(f, Sg, group, value, "3")).ToList();

    private static ChoiceOption Opt(string label, Func<AeoxContext, IReadOnlyList<Change>> changes) => new(label, changes);

    private static ChoiceSetting Pick(string id, TweakCategory category, string title, string description,
        Func<AeoxContext, string?>? recommended, params ChoiceOption[] options) =>
        new(id, category, title, description, string.Empty, new[] { ChoiceOption.Keep }.Concat(options).ToList(),
            ctx => options.Any(o => o.Changes(ctx).Count > 0), recommended);

    private static int Hz(AeoxContext c) => Math.Max(60, c.Hardware.RefreshRate);
    private static bool Weak(AeoxContext c) => c.Hardware.Tier == Aeox.Core.Hardware.GpuTier.Entry;
    private static bool NotFast(AeoxContext c) => c.Hardware.Tier != Aeox.Core.Hardware.GpuTier.Fast;
    private static string? ByVram(AeoxContext c) =>
        c.Hardware.IntegratedOnly || (c.Hardware.VramGb > 0 && c.Hardware.VramGb < 4.5) ? "Low"
        : c.Hardware.VramGb > 0 && c.Hardware.VramGb < 7.5 ? "Medium" : "High";
    private static Func<AeoxContext, bool> Always => _ => true;

    private static ChoiceSetting FpsCap(string id, Func<AeoxContext, int?, IReadOnlyList<Change>> write) =>
        Pick(id, TweakCategory.Performance, "Frame rate limit",
            "Uncapped gives the lowest delay. A cap at your monitor's refresh rate keeps fps steady and the PC cooler.",
            _ => "Uncapped",
            Opt("Uncapped", c => write(c, null)),
            Opt("Monitor refresh", c => write(c, Hz(c))),
            Opt("Double refresh", c => write(c, Hz(c) * 2)));

    private static ChoiceSetting UeTextures(string id, Func<AeoxContext, IEnumerable<string>> files) =>
        Pick(id, TweakCategory.Visuals, "Texture quality", "Pick what your video memory can hold. Too high causes stutter when it runs out.", ByVram,
            Opt("Low", c => Group(files(c), "sg.TextureQuality", "0")),
            Opt("Medium", c => Group(files(c), "sg.TextureQuality", "1")),
            Opt("High", c => Group(files(c), "sg.TextureQuality", "2")),
            Opt("Max", c => Group(files(c), "sg.TextureQuality", "3")));

    private static ChoiceSetting UeAntiAliasing(string id, Func<AeoxContext, IEnumerable<string>> files) =>
        Pick(id, TweakCategory.Visuals, "Anti-aliasing", "Low can look jagged at a distance, high can look soft. Medium is a good middle.", null,
            Opt("Low", c => Group(files(c), "sg.AntiAliasingQuality", "0")),
            Opt("Medium", c => Group(files(c), "sg.AntiAliasingQuality", "1")),
            Opt("High", c => Group(files(c), "sg.AntiAliasingQuality", "2")),
            Opt("Max", c => Group(files(c), "sg.AntiAliasingQuality", "3")));

    private static Func<AeoxContext, IReadOnlyList<ChoiceSetting>> UeChoices(string section) => ctx => new[]
    {
        FpsCap("ue-fpscap", (c, fps) => Ini(c.Game.SettingsFiles, section, ("FrameRateLimit", $"{fps ?? 0}.000000"))),
        UeTextures("ue-textures", c => c.Game.SettingsFiles),
        UeAntiAliasing("ue-aa", c => c.Game.SettingsFiles)
    };

    private static IEnumerable<Tweak> UeGroups(string prefix, Func<AeoxContext, IEnumerable<string>> files)
    {
        yield return Make(prefix + "-shadows", TweakCategory.Visuals, "Low shadows", "Shadows are one of the most expensive settings in this engine.",
            c => Group(files(c), "sg.ShadowQuality", "0"), Always);
        yield return Make(prefix + "-effects", TweakCategory.Visuals, "Low effects", "Smoke, explosions and particles cost less and block less of your view.",
            c => Group(files(c), "sg.EffectsQuality", "0"), Always);
        yield return Make(prefix + "-foliage", TweakCategory.Visuals, "Low foliage", "Less grass and bushes to draw.",
            c => Group(files(c), "sg.FoliageQuality", "0"), Always);
        yield return Make(prefix + "-post", TweakCategory.Visuals, "Low post processing", "Less bloom, haze and lens effects on top of the picture.",
            c => Group(files(c), "sg.PostProcessQuality", "0"), Always);
        yield return Make(prefix + "-reflections", TweakCategory.Visuals, "Low reflections", "Cheaper reflections on water, glass and floors.",
            c => files(c).Any(f => Doc(f)?.Get(Sg, "sg.ReflectionQuality") is not null) ? Group(files(c), "sg.ReflectionQuality", "0") : Array.Empty<Change>(), NotFast);
        yield return Make(prefix + "-gi", TweakCategory.Visuals, "Low lighting quality", "Cheaper global lighting. Big gain on mid-range cards.",
            c => files(c).Any(f => Doc(f)?.Get(Sg, "sg.GlobalIlluminationQuality") is not null) ? Group(files(c), "sg.GlobalIlluminationQuality", "0") : Array.Empty<Change>(), NotFast);
        yield return Make(prefix + "-view", TweakCategory.Visuals, "Lower view distance", "Less detail far away. Can hide small things at range, so only for weaker PCs.",
            c => Group(files(c), "sg.ViewDistanceQuality", "1"), Weak);
        yield return Make(prefix + "-shading", TweakCategory.Visuals, "Low shading", "Simpler materials and surfaces.",
            c => files(c).Any(f => Doc(f)?.Get(Sg, "sg.ShadingQuality") is not null) ? Group(files(c), "sg.ShadingQuality", "0") : Array.Empty<Change>(), Weak);
    }

    private static IEnumerable<string> RiotFiles(AeoxContext c) => Files(c, "RiotUserSettings.ini");
    private static IEnumerable<string> ShooterFiles(AeoxContext c) => Files(c, "GameUserSettings.ini");

    private static IReadOnlyList<Tweak> Valorant(AeoxContext ctx) => new[]
    {
        Make("val-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(ShooterFiles(c), Shooter, ("bUseVSync", "False")), Always),
        Make("val-reflex", TweakCategory.Performance, "NVIDIA Reflex + boost", "Shortest delay between click and screen.",
            c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::NvidiaReflexLowLatencySetting", "2")), Always, NvidiaTag, Nvidia),
        Make("val-dynres", TweakCategory.Performance, "No dynamic resolution", "The picture stays sharp instead of dropping resolution in fights.",
            c => Ini(ShooterFiles(c), Shooter, ("bUseDynamicResolution", "False")), Always),
        Make("val-menucap", TweakCategory.Performance, "Cap fps in menus and background", "Saves power and heat when you are not in a match.",
            c => IniHas(RiotFiles(c), Riot, ("EAresBoolSettingName::LimitFramerateInMenu", "True"), ("EAresBoolSettingName::LimitFramerateInBackground", "True")),
            c => c.Hardware.IsLaptop),
        Make("val-material", TweakCategory.Visuals, "Low material quality", "Simpler surfaces. Large fps gain, players stay just as visible.",
            c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::MaterialQuality", "0")), Always),
        Make("val-detail", TweakCategory.Visuals, "Low detail quality", "Less clutter on the map.",
            c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::DetailQuality", "0")), Always),
        Make("val-ui", TweakCategory.Visuals, "Low UI quality", "Lighter menus and HUD.",
            c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::UIQuality", "0")), Always),
        Make("val-shadows", TweakCategory.Visuals, "Shadows off", "Removes shadows. What most pros run.",
            c => Ini(RiotFiles(c), Riot, ("EAresBoolSettingName::ShadowsEnabled", "False")), Always),
        Make("val-bloom", TweakCategory.Visuals, "Bloom off", "No glow around bright light and abilities.",
            c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::BloomQuality", "0")), Always),
        Make("val-vignette", TweakCategory.Visuals, "Vignette off", "No dark corners on the screen.",
            c => Ini(RiotFiles(c), Riot, ("EAresBoolSettingName::VignetteEnabled", "False")), Always),
        Make("val-distortion", TweakCategory.Visuals, "Distortion off", "No wobble from abilities and heat.",
            c => Ini(RiotFiles(c), Riot, ("EAresBoolSettingName::DisableDistortion", "True")), Always),
        Make("val-gore", TweakCategory.Visuals, "No blood and corpses", "Less clutter after fights.",
            c => Ini(RiotFiles(c), Riot, ("EAresBoolSettingName::ShowBlood", "False"), ("EAresBoolSettingName::ShowCorpses", "False")))
    };

    private static IReadOnlyList<ChoiceSetting> ValorantChoices(AeoxContext ctx) => new[]
    {
        FpsCap("val-fpscap", (c, fps) => fps is null
            ? Ini(RiotFiles(c), Riot, ("EAresBoolSettingName::LimitFramerateAlways", "False")).Concat(Ini(ShooterFiles(c), Shooter, ("FrameRateLimit", "0.000000"))).ToList()
            : Ini(RiotFiles(c), Riot, ("EAresBoolSettingName::LimitFramerateAlways", "True"), ("EAresFloatSettingName::MaxFramerateAlways", fps.Value.ToString()))),
        Pick("val-textures", TweakCategory.Visuals, "Texture quality", "Pick what your video memory can hold.", ByVram,
            Opt("Low", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::TextureQuality", "0"))),
            Opt("Medium", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::TextureQuality", "1"))),
            Opt("High", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::TextureQuality", "2")))),
        Pick("val-aa", TweakCategory.Visuals, "Anti-aliasing", "None is sharpest and fastest. MSAA smooths edges for a small cost.",
            c => Weak(c) ? "None" : "MSAA 2x",
            Opt("None", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::AntiAliasing", "0"))),
            Opt("FXAA", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::AntiAliasing", "1"))),
            Opt("MSAA 2x", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::AntiAliasing", "2"))),
            Opt("MSAA 4x", c => Ini(RiotFiles(c), Riot, ("EAresIntSettingName::AntiAliasing", "3"))))
    };

    private static IEnumerable<string> Cs2Files(AeoxContext c) => Files(c, "cs2_video.txt");

    private static IReadOnlyList<Tweak> Cs2(AeoxContext ctx) => new[]
    {
        Make("cs2-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.mat_vsync", "0", "0")), Always),
        Make("cs2-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen. Boost on desktops.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.r_low_latency", c.Hardware.IsLaptop ? "1" : "2", "1")), Always, NvidiaTag, Nvidia),
        Make("cs2-fsr", TweakCategory.Performance, "FSR off", "Renders at full resolution so players stay sharp.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_fsr_detail", "0", "0")), Always),
        Make("cs2-shaders", TweakCategory.Visuals, "Low shader detail", "Simpler surfaces, more fps.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.shaderquality", "0", "1")), Always),
        Make("cs2-particles", TweakCategory.Visuals, "Low particle detail", "Smokes and molotovs cost less.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_particle_detail", "0", "1")), Always),
        Make("cs2-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners. Free fps.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_ao_detail", "0", "1")), Always),
        Make("cs2-hdr", TweakCategory.Visuals, "HDR set to performance", "Cheaper lighting with the same visibility.",
            c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_hdr_detail", "3", "-1")), Always)
    };

    private static IReadOnlyList<ChoiceSetting> Cs2Choices(AeoxContext ctx) => new[]
    {
        Pick("cs2-msaa", TweakCategory.Visuals, "Anti-aliasing", "MSAA keeps edges clean. Higher costs fps on weaker cards.",
            c => Weak(c) ? "CMAA2" : "MSAA 4x",
            Opt("None", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.msaa_samples", "0", "4"), ("setting.r_csgo_cmaa_enable", "0", "0"))),
            Opt("CMAA2", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.msaa_samples", "0", "4"), ("setting.r_csgo_cmaa_enable", "1", "0"))),
            Opt("MSAA 2x", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.msaa_samples", "2", "4"), ("setting.r_csgo_cmaa_enable", "0", "0"))),
            Opt("MSAA 4x", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.msaa_samples", "4", "4"), ("setting.r_csgo_cmaa_enable", "0", "0"))),
            Opt("MSAA 8x", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.msaa_samples", "8", "4"), ("setting.r_csgo_cmaa_enable", "0", "0")))),
        Pick("cs2-shadows", TweakCategory.Visuals, "Shadow quality", "High shows player shadows around corners. Low gives more fps.",
            c => Weak(c) ? "Low" : "High",
            Opt("Low", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_shadow_quality", "0", "1"))),
            Opt("Medium", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_shadow_quality", "1", "1"))),
            Opt("High", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_shadow_quality", "2", "1")))),
        Pick("cs2-textures", TweakCategory.Visuals, "Texture detail", "Pick what your video memory can hold.", ByVram,
            Opt("Low", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_texture_detail", "0", "1"))),
            Opt("Medium", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_texture_detail", "1", "1"))),
            Opt("High", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.videocfg_texture_detail", "2", "1")))),
        Pick("cs2-filtering", TweakCategory.Visuals, "Texture filtering", "Keeps floors and walls sharp at an angle. Almost free on modern cards.",
            c => Weak(c) ? "Bilinear" : "Anisotropic 16x",
            Opt("Bilinear", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.r_texturefilteringquality", "0", "3"))),
            Opt("Anisotropic 4x", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.r_texturefilteringquality", "3", "3"))),
            Opt("Anisotropic 16x", c => Text(Cs2Files(c), TextSettings.Quoted, ("setting.r_texturefilteringquality", "5", "3"))))
    };

    private static IEnumerable<string> ApexFiles(AeoxContext c) => Files(c, "videoconfig.txt");

    private static IReadOnlyList<Tweak> Apex(AeoxContext ctx) => new[]
    {
        Make("apex-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.mat_vsync_mode", "0", "0")), Always),
        Make("apex-dvs", TweakCategory.Performance, "No dynamic resolution", "The picture stays sharp instead of dropping resolution in fights.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.dvs_enable", "0", "0")), Always),
        Make("apex-ragdolls", TweakCategory.Performance, "Fewer ragdolls and gibs", "Less physics work after fights.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.cl_ragdoll_maxcount", "0", "8"), ("setting.cl_gib_allow", "0", "1")), Always),
        Make("apex-decals", TweakCategory.Performance, "Fewer impact marks", "Less bullet holes and decals to draw.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.r_createmodeldecals", "0", "1"), ("setting.r_decals", "0", "256")), NotFast),
        Make("apex-particles", TweakCategory.Performance, "Low effects detail", "Abilities and explosions cost less CPU.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.particle_cpu_level", "0", "2")), Always),
        Make("apex-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners. Free fps.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.ssao_enabled", "0", "1"), ("setting.ssao_quality", "0", "2")), Always),
        Make("apex-sun", TweakCategory.Visuals, "Light sun shadows", "Shorter, lower resolution sun shadows.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.csm_coverage", "0", "1"), ("setting.csm_cascade_res", "512", "1024")), Always),
        Make("apex-spot", TweakCategory.Visuals, "Spot shadows off", "No shadows from small lights indoors.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.shadow_enable", "0", "1")), Always),
        Make("apex-volumetric", TweakCategory.Visuals, "Volumetric lighting off", "No light shafts and haze.",
            c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.volumetric_lighting", "0", "1")), Always)
    };

    private static IReadOnlyList<ChoiceSetting> ApexChoices(AeoxContext ctx) => new[]
    {
        Pick("apex-aa", TweakCategory.Visuals, "Anti-aliasing", "TSAA smooths edges but looks a bit soft. None is sharpest.", null,
            Opt("None", c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.mat_antialias_mode", "0", "12"))),
            Opt("TSAA", c => Text(ApexFiles(c), TextSettings.Quoted, ("setting.mat_antialias_mode", "12", "12"))))
    };

    private static IEnumerable<string> CodFiles(AeoxContext c) => FilesLike(c, "options.");

    private static IReadOnlyList<Tweak> Cod(AeoxContext ctx) => new[]
    {
        Make("cod-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(CodFiles(c), TextSettings.Cst, ("VSync", "disabled", "disabled")), Always),
        Make("cod-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(CodFiles(c), TextSettings.Cst, ("NvidiaReflex", "Enabled", "Enabled")), Always, NvidiaTag, Nvidia),
        Make("cod-dynres", TweakCategory.Performance, "No dynamic resolution", "The picture stays sharp instead of dropping resolution.",
            c => Text(CodFiles(c), TextSettings.Cst, ("DynamicSceneResolution", "false", "false")), Always),
        Make("cod-menufps", TweakCategory.Performance, "Low fps in menus and alt-tab", "Saves power and heat outside of matches.",
            c => Text(CodFiles(c), TextSettings.Cst, ("MaxFpsInMenu", "60", "120"), ("MaxFpsOutOfFocus", "30", "30")), Always),
        Make("cod-raytracing", TweakCategory.Performance, "Ray tracing off", "Large fps gain, no visibility loss.",
            c => Text(CodFiles(c), TextSettings.Cst, ("DxrMode", "Off", "On")), Always),
        Make("cod-blur", TweakCategory.Visuals, "No blur", "Turns off motion blur and depth of field.",
            c => Text(CodFiles(c), TextSettings.Cst, ("EnableVelocityBasedBlur", "false", "true"), ("DepthOfField", "false", "true")), Always),
        Make("cod-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners.",
            c => Text(CodFiles(c), TextSettings.Cst, ("SSAOTechnique", "Off", "GTAO & MDAO")), Always),
        Make("cod-ssr", TweakCategory.Visuals, "Screen space reflections off", "Cheaper and less distracting reflections.",
            c => Text(CodFiles(c), TextSettings.Cst, ("SSRMode", "Off", "Deferred HQ")), Always),
        Make("cod-sss", TweakCategory.Visuals, "Screen space shadows off", "Removes small contact shadows.",
            c => Text(CodFiles(c), TextSettings.Cst, ("ScreenSpaceShadowQuality", "Off", "High")), NotFast),
        Make("cod-particles", TweakCategory.Visuals, "Low particle quality", "Smoke and explosions cost less.",
            c => Text(CodFiles(c), TextSettings.Cst, ("ParticleQuality", "low", "high")), Always),
        Make("cod-tessellation", TweakCategory.Visuals, "Tessellation off", "Flatter ground detail, more fps.",
            c => Text(CodFiles(c), TextSettings.Cst, ("Tessellation", "0_Off", "2_All")), Always),
        Make("cod-clutter", TweakCategory.Visuals, "Less clutter", "No shell casings or bullet impact effects.",
            c => Text(CodFiles(c), TextSettings.Cst, ("ShowBrass", "false", "true"), ("BulletImpacts", "false", "true")), Always),
        Make("cod-water", TweakCategory.Visuals, "Water caustics off", "No light patterns under water.",
            c => Text(CodFiles(c), TextSettings.Cst, ("WaterCausticsMode", "Off", "Low Quality")), Always)
    };

    private static IReadOnlyList<ChoiceSetting> CodChoices(AeoxContext ctx) => new[]
    {
        FpsCap("cod-fpscap", (c, fps) => fps is null
            ? Text(CodFiles(c), TextSettings.Cst, ("CapFps", "false", "false"))
            : Text(CodFiles(c), TextSettings.Cst, ("CapFps", "true", "false"), ("MaxFpsInGame", fps.Value.ToString(), "250")))
    };

    private static IEnumerable<string> Bf6Files(AeoxContext c) => Files(c, "PROFSAVE_profile");

    private static IReadOnlyList<Tweak> Bf6(AeoxContext ctx) => new[]
    {
        Make("bf6-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit in matches.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.FrameRateLimiterEnable", "0", "0")), Always),
        Make("bf6-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.VSyncMode", "0", "0")), Always),
        Make("bf6-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.NvidiaLowLatency", "1", "1")), Always, NvidiaTag, Nvidia),
        Make("bf6-blur", TweakCategory.Visuals, "Motion blur off", "Clean picture when turning.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.MotionBlurWorld", "0.000000", "1.000000"), ("GstRender.MotionBlurWeapon", "0.000000", "1.000000")), Always),
        Make("bf6-grain", TweakCategory.Visuals, "Film grain off", "No noise over the picture.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.FilmGrain", "0", "1")), Always),
        Make("bf6-vignette", TweakCategory.Visuals, "Vignette off", "No dark corners.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.Vignette", "0", "1")), Always),
        Make("bf6-chromatic", TweakCategory.Visuals, "Chromatic aberration off", "No color fringes at the edges.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.ChromaticAberration", "0", "1")), Always),
        Make("bf6-lens", TweakCategory.Visuals, "Lens distortion off", "Straight lines stay straight.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.LensDistortion", "0", "1")), Always),
        Make("bf6-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners.",
            c => Text(Bf6Files(c), TextSettings.Space, ("GstRender.AmbientOcclusion", "0", "1")), NotFast)
    };

    private static IEnumerable<string> OwFiles(AeoxContext c) => Files(c, "Settings_v0.ini");

    private static IReadOnlyList<Tweak> Overwatch(AeoxContext ctx) => new[]
    {
        Make("ow-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Render./VerticalSyncEnabled", "0", "0")), Always),
        Make("ow-dynres", TweakCategory.Performance, "No dynamic render scale", "The picture stays sharp instead of dropping resolution.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Render./DynamicRenderScale", "0", "0")), Always),
        Make("ow-tick", TweakCategory.Performance, "High precision mouse input", "Shots register at the exact moment you click, not the next frame.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Input./HighTickInput", "1", "-1")), Always),
        Make("ow-triple", TweakCategory.Performance, "Triple buffering off", "One frame less delay.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Render./TripleBufferingEnabled", "0", "0")), Always),
        Make("ow-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Render./SSAODetail", "0", "2")), Always),
        Make("ow-reflections", TweakCategory.Visuals, "Local reflections off", "Cheaper floors and walls.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Render./LocalReflections", "0", "1")), Always),
        Make("ow-aa", TweakCategory.Visuals, "Anti-aliasing off", "Sharpest picture, a little more fps.",
            c => Text(OwFiles(c), TextSettings.LooseIni, ("Render./AADetail", "0", "2")), Weak)
    };

    private static IReadOnlyList<ChoiceSetting> OverwatchChoices(AeoxContext ctx) => new[]
    {
        Pick("ow-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Boost keeps the GPU clocked up for the lowest delay.",
            c => c.Hardware.HasNvidia ? (c.Hardware.IsLaptop ? "On" : "On + boost") : null,
            Opt("Off", c => c.Hardware.HasNvidia ? Text(OwFiles(c), TextSettings.LooseIni, ("Render./ReflexMode", "0", "1")) : Array.Empty<Change>()),
            Opt("On", c => c.Hardware.HasNvidia ? Text(OwFiles(c), TextSettings.LooseIni, ("Render./ReflexMode", "1", "1")) : Array.Empty<Change>()),
            Opt("On + boost", c => c.Hardware.HasNvidia ? Text(OwFiles(c), TextSettings.LooseIni, ("Render./ReflexMode", "2", "1")) : Array.Empty<Change>()))
    };

    private static IEnumerable<string> GtaFiles(AeoxContext c) => Files(c, "settings.xml");

    private static IReadOnlyList<Tweak> Gta(AeoxContext ctx) => new[]
    {
        Make("gta-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("VSync", "0", "1")), Always),
        Make("gta-pause", TweakCategory.Performance, "Keep running when alt-tabbed", "The game does not freeze when you click away.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("PauseOnFocusLoss", "0", "1"))),
        Make("gta-blur", TweakCategory.Visuals, "No motion blur", "Cleaner picture when turning and driving.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("MotionBlurStrength", "0.000000", "0.000000")), Always),
        Make("gta-dof", TweakCategory.Visuals, "Depth of field off", "No blurry background.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("DoF", "false", "true")), Always),
        Make("gta-msaa", TweakCategory.Visuals, "MSAA off", "Biggest single fps cost in the game.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("MSAA", "0", "0"), ("ReflectionMSAA", "0", "0")), NotFast),
        Make("gta-grass", TweakCategory.Visuals, "Low grass", "Huge gain in the countryside.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("GrassQuality", "0", "1")), Always),
        Make("gta-post", TweakCategory.Visuals, "Low post effects", "Less bloom and haze.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("PostFX", "0", "1")), NotFast),
        Make("gta-ultrashadows", TweakCategory.Visuals, "No extra shadow detail", "Turns off high detail, long and particle shadows.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("UltraShadows_Enabled", "false", "false"), ("Shadow_LongShadows", "false", "false"), ("Shadow_ParticleShadows", "false", "true")), Always),
        Make("gta-ssao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("SSAO", "0", "1")), NotFast),
        Make("gta-tess", TweakCategory.Visuals, "Tessellation off", "Flatter surfaces, more fps.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("Tessellation", "0", "1")), NotFast),
        Make("gta-particles", TweakCategory.Visuals, "Low particles and water", "Cheaper explosions and water.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("ParticleQuality", "0", "1"), ("WaterQuality", "0", "1")), Weak),
        Make("gta-density", TweakCategory.Performance, "Less traffic and people", "Lower population density takes load off the CPU.",
            c => Text(GtaFiles(c), TextSettings.XmlValue, ("CityDensity", "0.500000", "1.000000"), ("PedVarietyMultiplier", "0.500000", "1.000000"), ("VehicleVarietyMultiplier", "0.500000", "1.000000")), Weak)
    };

    private static IReadOnlyList<ChoiceSetting> GtaChoices(AeoxContext ctx) => new[]
    {
        Pick("gta-shadows", TweakCategory.Visuals, "Shadow quality", "Shadows are heavy in the city.", c => Weak(c) ? "Normal" : "High",
            Opt("Normal", c => Text(GtaFiles(c), TextSettings.XmlValue, ("ShadowQuality", "1", "1"))),
            Opt("High", c => Text(GtaFiles(c), TextSettings.XmlValue, ("ShadowQuality", "2", "1"))),
            Opt("Very high", c => Text(GtaFiles(c), TextSettings.XmlValue, ("ShadowQuality", "3", "1")))),
        Pick("gta-textures", TweakCategory.Visuals, "Texture quality", "Pick what your video memory can hold.",
            c => ByVram(c) switch { "Low" => "Normal", "Medium" => "High", _ => "Very high" },
            Opt("Normal", c => Text(GtaFiles(c), TextSettings.XmlValue, ("TextureQuality", "0", "1"))),
            Opt("High", c => Text(GtaFiles(c), TextSettings.XmlValue, ("TextureQuality", "1", "1"))),
            Opt("Very high", c => Text(GtaFiles(c), TextSettings.XmlValue, ("TextureQuality", "2", "1"))))
    };

    private static IEnumerable<string> D2Files(AeoxContext c) => Files(c, "cvars.xml");

    private static IReadOnlyList<Tweak> Destiny(AeoxContext ctx) => new[]
    {
        Make("d2-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("low_latency_mode", "1", "0")), Always, NvidiaTag, Nvidia),
        Make("d2-smoothing", TweakCategory.Performance, "Mouse smoothing off", "Your aim follows the mouse exactly with no smoothing.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("mouse_smoothing_mode", "0", "2")), Always),
        Make("d2-res", TweakCategory.Performance, "Full render resolution", "Renders at 100% so targets stay sharp.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("render_resolution_percentage", "100", "100")), Always),
        Make("d2-dof", TweakCategory.Visuals, "Depth of field off", "No blurry background.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("dof_mode", "0", "3")), Always),
        Make("d2-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("ssao_mode", "0", "2")), Always),
        Make("d2-wind", TweakCategory.Visuals, "Wind sway off", "Grass and trees stop moving.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("wind_impulse", "0", "1")), Always),
        Make("d2-foliage", TweakCategory.Visuals, "Low foliage and foliage shadows", "Less plants to draw.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("foliage_detail", "0", "2"), ("foliage_shadows_mode", "0", "2")), Always),
        Make("d2-lights", TweakCategory.Visuals, "Light shadows off", "No shadows from small lights.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("local_light_shadows", "0", "1")), Always),
        Make("d2-detail", TweakCategory.Visuals, "Low environment and character detail", "Less detail at range.",
            c => Text(D2Files(c), TextSettings.XmlCvar, ("environment_detail", "0", "2"), ("character_detail", "0", "2")), Weak)
    };

    private static IReadOnlyList<ChoiceSetting> DestinyChoices(AeoxContext ctx) => new[]
    {
        FpsCap("d2-fpscap", (c, fps) => fps is null
            ? Text(D2Files(c), TextSettings.XmlCvar, ("framerate_cap_enabled", "0", "1"))
            : Text(D2Files(c), TextSettings.XmlCvar, ("framerate_cap_enabled", "1", "1"), ("framerate_cap", fps.Value.ToString(), "60"))),
        Pick("d2-shadows", TweakCategory.Visuals, "Shadow quality", "Shadows cost a lot in big areas.", c => Weak(c) ? "Low" : "Medium",
            Opt("Low", c => Text(D2Files(c), TextSettings.XmlCvar, ("shadow_quality", "0", "2"))),
            Opt("Medium", c => Text(D2Files(c), TextSettings.XmlCvar, ("shadow_quality", "2", "2"))),
            Opt("Highest", c => Text(D2Files(c), TextSettings.XmlCvar, ("shadow_quality", "4", "2")))),
        Pick("d2-aa", TweakCategory.Visuals, "Anti-aliasing", "SMAA is clean and cheap.", _ => "SMAA",
            Opt("Off", c => Text(D2Files(c), TextSettings.XmlCvar, ("anti_aliasing_mode", "0", "1"))),
            Opt("FXAA", c => Text(D2Files(c), TextSettings.XmlCvar, ("anti_aliasing_mode", "1", "1"))),
            Opt("SMAA", c => Text(D2Files(c), TextSettings.XmlCvar, ("anti_aliasing_mode", "2", "1"))))
    };

    private static IEnumerable<string> McFiles(AeoxContext c) => Files(c, "options.txt");

    private static IReadOnlyList<Tweak> Minecraft(AeoxContext ctx) => new[]
    {
        Make("mc-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor.",
            c => Text(McFiles(c), TextSettings.Colon, ("enableVsync", "false", "true")), Always),
        Make("mc-fps", TweakCategory.Performance, "Unlimited frame rate", "Max framerate set to unlimited.",
            c => Text(McFiles(c), TextSettings.Colon, ("maxFps", "260", "120")), Always),
        Make("mc-chunks", TweakCategory.Performance, "Threaded chunk updates", "Chunks load without stalling the game.",
            c => Text(McFiles(c), TextSettings.Colon, ("prioritizeChunkUpdates", "0", "0")), Always),
        Make("mc-entities", TweakCategory.Performance, "Shorter entity distance", "Mobs far away are not drawn.",
            c => Text(McFiles(c), TextSettings.Colon, ("entityDistanceScaling", "0.75", "1.0")), Weak),
        Make("mc-clouds", TweakCategory.Visuals, "Clouds off", "Clouds cost more than they look.",
            c => Text(McFiles(c), TextSettings.Colon, ("renderClouds", "\"false\"", "\"true\"")), Always),
        Make("mc-particles", TweakCategory.Visuals, "Minimal particles", "Less dust and smoke to draw.",
            c => Text(McFiles(c), TextSettings.Colon, ("particles", "2", "0")), Always),
        Make("mc-shadows", TweakCategory.Visuals, "Entity shadows off", "No round shadows under mobs.",
            c => Text(McFiles(c), TextSettings.Colon, ("entityShadows", "false", "true")), Always),
        Make("mc-biome", TweakCategory.Visuals, "No biome blending", "Hard color borders between biomes. Big gain on weak CPUs.",
            c => Text(McFiles(c), TextSettings.Colon, ("biomeBlendRadius", "0", "2")), Always),
        Make("mc-bob", TweakCategory.Visuals, "View bobbing off", "The camera stops swaying while walking.",
            c => Text(McFiles(c), TextSettings.Colon, ("bobView", "false", "true"))),
        Make("mc-fov", TweakCategory.Visuals, "No fov and screen effects", "Sprinting and potions no longer warp the view.",
            c => Text(McFiles(c), TextSettings.Colon, ("fovEffectScale", "0.0", "1.0"), ("screenEffectScale", "0.0", "1.0"))),
        Make("mc-vignette", TweakCategory.Visuals, "Vignette off", "No dark corners.",
            c => Text(McFiles(c), TextSettings.Colon, ("vignette", "false", "true")), Always),
        Make("mc-mipmaps", TweakCategory.Visuals, "Mipmaps off", "Slightly more fps, a bit grainier blocks far away.",
            c => Text(McFiles(c), TextSettings.Colon, ("mipmapLevels", "0", "4")), Weak),
        Make("mc-leaves", TweakCategory.Visuals, "Fast leaves", "Leaves render as solid blocks.",
            c => Text(McFiles(c), TextSettings.Colon, ("cutoutLeaves", "false", "true")), Weak)
    };

    private static IReadOnlyList<ChoiceSetting> MinecraftChoices(AeoxContext ctx) => new[]
    {
        Pick("mc-render", TweakCategory.Performance, "Render distance", "The biggest fps setting in the game.",
            c => Weak(c) ? "8 chunks" : NotFast(c) ? "12 chunks" : "16 chunks",
            Opt("8 chunks", c => Text(McFiles(c), TextSettings.Colon, ("renderDistance", "8", "12"))),
            Opt("12 chunks", c => Text(McFiles(c), TextSettings.Colon, ("renderDistance", "12", "12"))),
            Opt("16 chunks", c => Text(McFiles(c), TextSettings.Colon, ("renderDistance", "16", "12"))),
            Opt("24 chunks", c => Text(McFiles(c), TextSettings.Colon, ("renderDistance", "24", "12")))),
        Pick("mc-sim", TweakCategory.Performance, "Simulation distance", "How far away farms and mobs keep running. Lower takes load off the CPU.",
            c => Weak(c) ? "6 chunks" : "10 chunks",
            Opt("6 chunks", c => Text(McFiles(c), TextSettings.Colon, ("simulationDistance", "6", "12"))),
            Opt("10 chunks", c => Text(McFiles(c), TextSettings.Colon, ("simulationDistance", "10", "12"))),
            Opt("16 chunks", c => Text(McFiles(c), TextSettings.Colon, ("simulationDistance", "16", "12"))))
    };

    private static IReadOnlyList<Tweak> EmbarkGame(AeoxContext ctx, string blurKey, string blurOff) => new[]
    {
        Make("embark-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(c.Game.SettingsFiles, Embark, ("bUseVSync", "False")), Always),
        Make("embark-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Ini(c.Game.SettingsFiles, Embark, ("NvReflexMode", "Enabled")), Always, NvidiaTag, Nvidia),
        Make("embark-framegen", TweakCategory.Performance, "Frame generation off", "Fake frames add input delay. Real frames only.",
            c => IniHas(c.Game.SettingsFiles, Embark, ("DLSSFrameGenerationMode", "Off"), ("FSRFrameGenerationMode", "Off")), Always),
        Make("embark-gi", TweakCategory.Performance, "Static global illumination", "Dynamic RTXGI is very heavy. Static looks close and runs much faster.",
            c => IniHas(c.Game.SettingsFiles, Embark, ("RTXGIQuality", "Static")), Always),
        Make("embark-blur", TweakCategory.Visuals, "Motion blur off", "Clean picture when turning.",
            c => Ini(c.Game.SettingsFiles, Embark, (blurKey, blurOff)), Always),
        Make("embark-lens", TweakCategory.Visuals, "Lens distortion off", "Straight lines stay straight.",
            c => Ini(c.Game.SettingsFiles, Embark, ("LensDistortionEnabled", "False")), Always)
    }.Concat(UeGroups("embark", c => c.Game.SettingsFiles)).ToList();

    private static IReadOnlyList<Tweak> MarvelRivals(AeoxContext ctx) => new[]
    {
        Make("mr-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(c.Game.SettingsFiles, Marvel, ("bUseVSync", "False")), Always),
        Make("mr-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Ini(c.Game.SettingsFiles, Marvel, ("bNvidiaReflex", "True")), Always, NvidiaTag, Nvidia),
        Make("mr-framegen", TweakCategory.Performance, "Frame generation off", "Fake frames add input delay. Real frames only.",
            c => IniHas(c.Game.SettingsFiles, Marvel, ("bDlssFrameGeneration", "False"), ("bFSRFrameGeneration", "False"), ("bXeFrameGeneration", "False")), Always),
        Make("mr-scale", TweakCategory.Performance, "Full render resolution", "Renders at 100% so heroes stay sharp.",
            c => IniHas(c.Game.SettingsFiles, Marvel, ("ScreenPercentage", "100")), Always)
    }.Concat(UeGroups("mr", c => c.Game.SettingsFiles)).ToList();

    private static IReadOnlyList<Tweak> Pubg(AeoxContext ctx) => new[]
    {
        Make("pubg-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit in matches.",
            c => Ini(c.Game.SettingsFiles, Tsl, ("InGameFrameRateLimitType", "Unlimited")), Always),
        Make("pubg-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(c.Game.SettingsFiles, Tsl, ("bUseVSync", "False")), Always),
        Make("pubg-smooth", TweakCategory.Performance, "No smoothed frame rate", "No artificial frame pacing that adds delay.",
            c => IniHas(c.Game.SettingsFiles, Tsl, ("bUseInGameSmoothedFrameRate", "False")), Always),
        Make("pubg-scale", TweakCategory.Performance, "Full screen scale", "Renders at 100% so players at range stay sharp.",
            c => IniHas(c.Game.SettingsFiles, Tsl, ("ScreenScale", "100.000000")), Always),
        Make("pubg-blur", TweakCategory.Visuals, "No motion blur", "Cleaner picture when turning.",
            c => Ini(c.Game.SettingsFiles, Tsl, ("bMotionBlur", "False")), Always),
        Make("pubg-sharpen", TweakCategory.Visuals, "Sharpen on", "Crisper edges, makes players at range easier to see.",
            c => IniHas(c.Game.SettingsFiles, Tsl, ("bSharpen", "True")), Always),
        Make("pubg-shadows", TweakCategory.Visuals, "Very low shadows", "The most expensive setting in the game.",
            c => Group(c.Game.SettingsFiles, "sg.ShadowQuality", "0"), Always),
        Make("pubg-effects", TweakCategory.Visuals, "Very low effects", "Smoke and explosions cost less.",
            c => Group(c.Game.SettingsFiles, "sg.EffectsQuality", "0"), Always),
        Make("pubg-foliage", TweakCategory.Visuals, "Very low foliage", "Less grass and bushes. Players in grass are easier to spot.",
            c => Group(c.Game.SettingsFiles, "sg.FoliageQuality", "0"), Always),
        Make("pubg-post", TweakCategory.Visuals, "Very low post processing", "Less haze and bloom.",
            c => Group(c.Game.SettingsFiles, "sg.PostProcessQuality", "0"), Always),
        Make("pubg-view", TweakCategory.Visuals, "Low view distance", "Fewer buildings far away. Only for weaker PCs.",
            c => Group(c.Game.SettingsFiles, "sg.ViewDistanceQuality", "1"), Weak)
    };

    private static IReadOnlyList<ChoiceSetting> PubgChoices(AeoxContext ctx) => new[]
    {

        Pick("pubg-textures", TweakCategory.Visuals, "Texture quality", "Pick what your video memory can hold.", ByVram,
            Opt("Low", c => Group(c.Game.SettingsFiles, "sg.TextureQuality", "1")),
            Opt("Medium", c => Group(c.Game.SettingsFiles, "sg.TextureQuality", "2")),
            Opt("High", c => Group(c.Game.SettingsFiles, "sg.TextureQuality", "3"))),
        Pick("pubg-aa", TweakCategory.Visuals, "Anti-aliasing", "Ultra anti-aliasing is what most pros run in this game.", c => Weak(c) ? "Medium" : "Ultra",
            Opt("Medium", c => Group(c.Game.SettingsFiles, "sg.AntiAliasingQuality", "2")),
            Opt("Ultra", c => Group(c.Game.SettingsFiles, "sg.AntiAliasingQuality", "4")))
    };

    private static IReadOnlyList<Tweak> Siege(AeoxContext ctx) => new[]
    {
        Make("r6-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("VSync", "0", "0")), Always),
        Make("r6-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("FPSLimit", "0", "0")), Always),
        Make("r6-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading in corners.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("AmbientOcclusion", "0", "1")), Always),
        Make("r6-lens", TweakCategory.Visuals, "Lens effects off", "No flares and glare.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("LensEffects", "0", "1")), Always),
        Make("r6-dof", TweakCategory.Visuals, "Zoom-in depth of field off", "The background stays sharp when you aim.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("ZoomInDepthOfField", "0", "1")), Always)
    };

    private static IReadOnlyList<Tweak> RocketLeague(AeoxContext ctx) => new[]
    {
        Make("rl-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("UseVsync", "False", "False")), Always),
        Make("rl-blur", TweakCategory.Visuals, "Motion blur off", "Clean picture at high speed.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("MotionBlur", "False", "True")), Always),
        Make("rl-dof", TweakCategory.Visuals, "Depth of field off", "No blurry background.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("DepthOfField", "False", "True")), Always),
        Make("rl-bloom", TweakCategory.Visuals, "Bloom and light shafts off", "No glow and sun rays.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("Bloom", "False", "True"), ("LightShafts", "False", "True"), ("LensFlares", "False", "True")), Always),
        Make("rl-ao", TweakCategory.Visuals, "Ambient occlusion off", "Removes soft shading.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("AmbientOcclusion", "False", "True")), Always),
        Make("rl-shadows", TweakCategory.Visuals, "Dynamic shadows off", "Cars no longer cast shadows. Big fps gain on weak PCs.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("DynamicShadows", "False", "True")), Weak)
    };

    private static IReadOnlyList<Tweak> League(AeoxContext ctx) => new[]
    {
        Make("lol-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("WaitForVerticalSync", "0", "0")), Always),
        Make("lol-hud", TweakCategory.Performance, "HUD animations off", "Snappier interface.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("EnableHUDAnimations", "0", "1")), Always),
        Make("lol-shadows", TweakCategory.Visuals, "Shadows off", "The biggest fps cost in team fights.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("ShadowQuality", "0", "2")), NotFast),
        Make("lol-grass", TweakCategory.Visuals, "Grass swaying off", "Less movement on the map.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("EnableGrassSwaying", "0", "1")), Always),
        Make("lol-fxaa", TweakCategory.Visuals, "Anti-aliasing off", "Sharper picture, more fps.",
            c => Text(c.Game.SettingsFiles, TextSettings.LooseIni, ("EnableFXAA", "0", "1")), Weak)
    };
}
