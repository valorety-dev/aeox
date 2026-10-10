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
    string? Note = null);

public static class OtherGames
{
    private const string Sg = "ScalabilityGroups";
    private const string Embark = "/Script/EmbarkUserSettings.EmbarkGameUserSettings";
    private const string Marvel = "/Script/Marvel.MarvelGameUserSettings";
    private const string Tsl = "/Script/TslGame.TslGameUserSettings";
    private const string Shooter = "/Script/ShooterGame.ShooterGameUserSettings";
    private const string Riot = "Settings";

    private static readonly string[] CompetitiveGroups =
    {
        "sg.ShadowQuality", "sg.PostProcessQuality", "sg.EffectsQuality", "sg.FoliageQuality", "sg.ReflectionQuality", "sg.GlobalIlluminationQuality"
    };

    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static IReadOnlyList<GameDef> All { get; } = new List<GameDef>
    {
        new("valorant", "Valorant", new[] { "VALORANT-Win64-Shipping.exe" }, Array.Empty<string>(),
            () => Glob(Path.Combine(Local, "VALORANT", "Saved", "Config"), "GameUserSettings.ini", "RiotUserSettings.ini"),
            Valorant, "Valorant also keeps settings on your Riot account. Change them while the game is closed."),
        new("cs2", "Counter-Strike 2", new[] { "cs2.exe" }, new[] { @"Counter-Strike Global Offensive\game\bin\win64\cs2.exe" },
            () => SteamUserFiles(@"730\local\cfg\cs2_video.txt"), Cs2),
        new("apex", "Apex Legends", new[] { "r5apex.exe", "r5apex_dx12.exe" }, new[] { @"Apex Legends\r5apex.exe" },
            () => Existing(Path.Combine(Home, "Saved Games", "Respawn", "Apex", "local", "videoconfig.txt")), Apex),
        new("cod", "Call of Duty", new[] { "cod.exe", "cod24-cod.exe", "cod25-cod.exe" }, new[] { @"Call of Duty HQ\cod.exe" },
            () => Directory.Exists(Path.Combine(Docs, "Call of Duty", "players"))
                ? Directory.EnumerateFiles(Path.Combine(Docs, "Call of Duty", "players"), "options.4.cod*.cst").ToList()
                : Array.Empty<string>(), Cod),
        new("bf6", "Battlefield 6", new[] { "bf6.exe" }, Array.Empty<string>(),
            () => Existing(Path.Combine(Docs, "Battlefield 6", "settings", "PROFSAVE_profile")), Bf6),
        new("overwatch", "Overwatch 2", new[] { "Overwatch.exe" }, Array.Empty<string>(),
            () => Existing(Path.Combine(Docs, "Overwatch", "Settings", "Settings_v0.ini")), Overwatch),
        new("gta5", "GTA V", new[] { "GTA5.exe", "GTA5_Enhanced.exe" }, new[] { @"Grand Theft Auto V\GTA5.exe", @"Grand Theft Auto V Enhanced\GTA5_Enhanced.exe" },
            () => Existing(Path.Combine(Docs, "Rockstar Games", "GTA V", "settings.xml"), Path.Combine(Docs, "Rockstar Games", "GTAV Enhanced", "settings.xml")), Gta),
        new("destiny2", "Destiny 2", new[] { "destiny2.exe" }, new[] { @"Destiny 2\destiny2.exe" },
            () => Existing(Path.Combine(Roaming, "Bungie", "DestinyPC", "prefs", "cvars.xml")), Destiny),
        new("minecraft", "Minecraft", new[] { "javaw.exe" }, Array.Empty<string>(),
            () => Existing(Path.Combine(Roaming, ".minecraft", "options.txt")), Minecraft),
        new("thefinals", "The Finals", new[] { "Discovery.exe" }, new[] { @"The Finals\Discovery\Binaries\Win64\Discovery.exe" },
            () => Existing(Path.Combine(Local, "Discovery", "Saved", "Config", "WindowsClient", "GameUserSettings.ini")),
            ctx => EmbarkGame(ctx, "MotionBlurEnabled", "False")),
        new("arcraiders", "Arc Raiders", new[] { "PioneerGame.exe" }, new[] { @"Arc Raiders\PioneerGame.exe" },
            () => Existing(Path.Combine(Local, "PioneerGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini")),
            ctx => EmbarkGame(ctx, "MotionBlurMode", "Off")),
        new("marvelrivals", "Marvel Rivals", new[] { "Marvel-Win64-Shipping.exe" }, new[] { @"MarvelRivals\MarvelGame\Marvel\Binaries\Win64\Marvel-Win64-Shipping.exe" },
            () => Existing(Path.Combine(Local, "Marvel", "Saved", "Config", "Windows", "GameUserSettings.ini")), MarvelRivals),
        new("pubg", "PUBG", new[] { "TslGame.exe" }, new[] { @"PUBG\TslGame\Binaries\Win64\TslGame.exe" },
            () => Existing(Path.Combine(Local, "TslGame", "Saved", "Config", "WindowsNoEditor", "GameUserSettings.ini")), Pubg),
        new("r6", "Rainbow Six Siege", new[] { "RainbowSix.exe", "RainbowSix_Vulkan.exe" }, new[] { @"Tom Clancy's Rainbow Six Siege\RainbowSix.exe" },
            NoFiles, null),
        new("rocketleague", "Rocket League", new[] { "RocketLeague.exe" }, new[] { @"rocketleague\Binaries\Win64\RocketLeague.exe" },
            NoFiles, null),
        new("deltaforce", "Delta Force", new[] { "DeltaForceClient-Win64-Shipping.exe" }, new[] { @"Delta Force\Game\DeltaForce\Binaries\Win64\DeltaForceClient-Win64-Shipping.exe" },
            NoFiles, null),
        new("arenabreakout", "Arena Breakout: Infinite", new[] { "UAGame.exe" }, new[] { @"ABInfinite\ABInfinite\Binaries\Win64\UAGame.exe" },
            NoFiles, null),
        new("rust", "Rust", new[] { "RustClient.exe" }, new[] { @"Rust\RustClient.exe" }, NoFiles, null),
        new("warthunder", "War Thunder", new[] { "aces.exe" }, new[] { @"War Thunder\win64\aces.exe" }, NoFiles, null),
        new("league", "League of Legends", new[] { "League of Legends.exe" }, Array.Empty<string>(), NoFiles, null)
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

    private static IReadOnlyList<Change> LowGroups(IEnumerable<string> files) =>
        files.SelectMany(f => CompetitiveGroups.Select(g => Change.Ini(f, Sg, g, "0", "3"))).ToList();

    private static IReadOnlyList<Tweak> Valorant(AeoxContext ctx) => new[]
    {
        Make("val-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit in matches.",
            c => Ini(Files(c, "RiotUserSettings.ini"), Riot, ("EAresBoolSettingName::LimitFramerateAlways", "False"))
                .Concat(Ini(Files(c, "GameUserSettings.ini"), Shooter, ("FrameRateLimit", "0.000000"))).ToList()),
        Make("val-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(Files(c, "GameUserSettings.ini"), Shooter, ("bUseVSync", "False"))),
        Make("val-competitive", TweakCategory.Visuals, "Competitive graphics",
            "Low materials, details and UI. No shadows, bloom, vignette or distortion. What most pros run.",
            c => Ini(Files(c, "RiotUserSettings.ini"), Riot,
                ("EAresIntSettingName::MaterialQuality", "0"), ("EAresIntSettingName::DetailQuality", "0"),
                ("EAresIntSettingName::UIQuality", "0"), ("EAresIntSettingName::BloomQuality", "0"),
                ("EAresBoolSettingName::ShadowsEnabled", "False"), ("EAresBoolSettingName::VignetteEnabled", "False"),
                ("EAresBoolSettingName::DisableDistortion", "True"))),
        Make("val-textures", TweakCategory.Visuals, "Low textures", "Saves video memory on cards with little VRAM.",
            c => Ini(Files(c, "RiotUserSettings.ini"), Riot, ("EAresIntSettingName::TextureQuality", "0")),
            c => c.Hardware.Tier == Aeox.Core.Hardware.GpuTier.Entry)
    };

    private static IReadOnlyList<Tweak> Cs2(AeoxContext ctx) => new[]
    {
        Make("cs2-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Files(c, "cs2_video.txt"), TextSettings.Quoted, ("setting.mat_vsync", "0", "0"))),
        Make("cs2-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen. Boost on desktops.",
            c => Text(Files(c, "cs2_video.txt"), TextSettings.Quoted, ("setting.r_low_latency", c.Hardware.IsLaptop ? "1" : "2", "1")),
            tag: NvidiaTag, extra: Nvidia),
        Make("cs2-competitive", TweakCategory.Visuals, "Competitive graphics",
            "Low shaders, particles and ambient occlusion, no FSR. Shadows stay so you still see players in corners.",
            c => Text(Files(c, "cs2_video.txt"), TextSettings.Quoted,
                ("setting.shaderquality", "0", "1"), ("setting.videocfg_particle_detail", "0", "1"),
                ("setting.videocfg_ao_detail", "0", "1"), ("setting.videocfg_fsr_detail", "0", "0")))
    };

    private static IReadOnlyList<Tweak> Apex(AeoxContext ctx) => new[]
    {
        Make("apex-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Files(c, "videoconfig.txt"), TextSettings.Quoted, ("setting.mat_vsync_mode", "0", "0"))),
        Make("apex-dvs", TweakCategory.Performance, "No dynamic resolution", "The picture stays sharp instead of dropping resolution in fights.",
            c => Text(Files(c, "videoconfig.txt"), TextSettings.Quoted, ("setting.dvs_enable", "0", "0"))),
        Make("apex-competitive", TweakCategory.Visuals, "Competitive graphics", "No ambient occlusion or TSAA, lighter sun shadows.",
            c => Text(Files(c, "videoconfig.txt"), TextSettings.Quoted,
                ("setting.ssao_enabled", "0", "1"), ("setting.mat_antialias_mode", "0", "12"),
                ("setting.csm_coverage", "0", "1"), ("setting.csm_cascade_res", "512", "1024")))
    };

    private static IReadOnlyList<Tweak> Cod(AeoxContext ctx) => new[]
    {
        Make("cod-fps", TweakCategory.Performance, "Uncapped frame rate", "No custom FPS limit in matches.",
            c => Text(FilesLike(c, "options."), TextSettings.Cst, ("CapFps", "false", "false"))),
        Make("cod-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(FilesLike(c, "options."), TextSettings.Cst, ("VSync", "disabled", "disabled"))),
        Make("cod-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(FilesLike(c, "options."), TextSettings.Cst, ("NvidiaReflex", "Enabled", "Enabled")),
            tag: NvidiaTag, extra: Nvidia),
        Make("cod-blur", TweakCategory.Visuals, "No blur", "Turns off motion blur and depth of field.",
            c => Text(FilesLike(c, "options."), TextSettings.Cst, ("EnableVelocityBasedBlur", "false", "true"), ("DepthOfField", "false", "true")))
    };

    private static IReadOnlyList<Tweak> Bf6(AeoxContext ctx) => new[]
    {
        Make("bf6-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit in matches.",
            c => Text(Files(c, "PROFSAVE_profile"), TextSettings.Space, ("GstRender.FrameRateLimiterEnable", "0", "0"))),
        Make("bf6-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Files(c, "PROFSAVE_profile"), TextSettings.Space, ("GstRender.VSyncMode", "0", "0"))),
        Make("bf6-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(Files(c, "PROFSAVE_profile"), TextSettings.Space, ("GstRender.NvidiaLowLatency", "1", "1")),
            tag: NvidiaTag, extra: Nvidia),
        Make("bf6-clean", TweakCategory.Visuals, "Clean picture", "No motion blur, film grain, vignette, chromatic aberration or lens distortion.",
            c => Text(Files(c, "PROFSAVE_profile"), TextSettings.Space,
                ("GstRender.MotionBlurWorld", "0.000000", "1.000000"), ("GstRender.MotionBlurWeapon", "0.000000", "1.000000"),
                ("GstRender.FilmGrain", "0", "1"), ("GstRender.Vignette", "0", "1"),
                ("GstRender.ChromaticAberration", "0", "1"), ("GstRender.LensDistortion", "0", "1")))
    };

    private static IReadOnlyList<Tweak> Overwatch(AeoxContext ctx) => new[]
    {
        Make("ow-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Files(c, "Settings_v0.ini"), TextSettings.LooseIni, ("Render./VerticalSyncEnabled", "0", "0"))),
        Make("ow-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(Files(c, "Settings_v0.ini"), TextSettings.LooseIni, ("Render./ReflexMode", "1", "1")),
            tag: NvidiaTag, extra: Nvidia),
        Make("ow-dynres", TweakCategory.Performance, "No dynamic render scale", "The picture stays sharp instead of dropping resolution.",
            c => Text(Files(c, "Settings_v0.ini"), TextSettings.LooseIni, ("Render./DynamicRenderScale", "0", "0"))),
        Make("ow-competitive", TweakCategory.Visuals, "Competitive graphics", "No ambient occlusion or local reflections.",
            c => Text(Files(c, "Settings_v0.ini"), TextSettings.LooseIni, ("Render./SSAODetail", "0", "2"), ("Render./LocalReflections", "0", "1")))
    };

    private static IReadOnlyList<Tweak> Gta(AeoxContext ctx) => new[]
    {
        Make("gta-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Text(Files(c, "settings.xml"), TextSettings.XmlValue, ("VSync", "0", "1"))),
        Make("gta-blur", TweakCategory.Visuals, "No motion blur", "Cleaner picture when turning and driving.",
            c => Text(Files(c, "settings.xml"), TextSettings.XmlValue, ("MotionBlurStrength", "0.000000", "0.000000"))),
        Make("gta-light", TweakCategory.Visuals, "Lighter graphics", "No MSAA, lower grass and post effects. Big FPS gain in the city.",
            c => Text(Files(c, "settings.xml"), TextSettings.XmlValue,
                ("MSAA", "0", "0"), ("ReflectionMSAA", "0", "0"), ("GrassQuality", "0", "1"), ("PostFX", "0", "1")))
    };

    private static IReadOnlyList<Tweak> Destiny(AeoxContext ctx) => new[]
    {
        Make("d2-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit.",
            c => Text(Files(c, "cvars.xml"), TextSettings.XmlCvar, ("framerate_cap_enabled", "0", "1"))),
        Make("d2-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Text(Files(c, "cvars.xml"), TextSettings.XmlCvar, ("low_latency_mode", "1", "0")),
            tag: NvidiaTag, extra: Nvidia),
        Make("d2-clean", TweakCategory.Visuals, "Clean picture", "No depth of field, ambient occlusion or wind sway.",
            c => Text(Files(c, "cvars.xml"), TextSettings.XmlCvar, ("dof_mode", "0", "3"), ("ssao_mode", "0", "2"), ("wind_impulse", "0", "1")))
    };

    private static IReadOnlyList<Tweak> Minecraft(AeoxContext ctx) => new[]
    {
        Make("mc-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor.",
            c => Text(Files(c, "options.txt"), TextSettings.Colon, ("enableVsync", "false", "true"))),
        Make("mc-fps", TweakCategory.Performance, "Unlimited frame rate", "Max framerate set to unlimited.",
            c => Text(Files(c, "options.txt"), TextSettings.Colon, ("maxFps", "260", "120"))),
        Make("mc-light", TweakCategory.Visuals, "Lighter graphics", "No clouds or entity shadows, minimal particles, no biome blending.",
            c => Text(Files(c, "options.txt"), TextSettings.Colon,
                ("renderClouds", "\"false\"", "\"true\""), ("particles", "2", "0"), ("entityShadows", "false", "true"), ("biomeBlendRadius", "0", "2")))
    };

    private static IReadOnlyList<Tweak> EmbarkGame(AeoxContext ctx, string blurKey, string blurOff) => new[]
    {
        Make("embark-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit.",
            c => Ini(c.Game.SettingsFiles, Embark, ("FrameRateLimit", "0.000000"))),
        Make("embark-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(c.Game.SettingsFiles, Embark, ("bUseVSync", "False"))),
        Make("embark-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Ini(c.Game.SettingsFiles, Embark, ("NvReflexMode", "Enabled")), tag: NvidiaTag, extra: Nvidia),
        Make("embark-clean", TweakCategory.Visuals, "Clean picture", "No motion blur or lens distortion.",
            c => Ini(c.Game.SettingsFiles, Embark, (blurKey, blurOff), ("LensDistortionEnabled", "False"))),
        Make("embark-competitive", TweakCategory.Visuals, "Competitive graphics", "Low shadows, effects, foliage, reflections and lighting. Textures stay.",
            c => LowGroups(c.Game.SettingsFiles))
    };

    private static IReadOnlyList<Tweak> MarvelRivals(AeoxContext ctx) => new[]
    {
        Make("mr-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit.",
            c => Ini(c.Game.SettingsFiles, Marvel, ("FrameRateLimit", "0.000000"))),
        Make("mr-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(c.Game.SettingsFiles, Marvel, ("bUseVSync", "False"))),
        Make("mr-reflex", TweakCategory.Performance, "NVIDIA Reflex", "Shortest delay between click and screen.",
            c => Ini(c.Game.SettingsFiles, Marvel, ("bNvidiaReflex", "True")), tag: NvidiaTag, extra: Nvidia),
        Make("mr-competitive", TweakCategory.Visuals, "Competitive graphics", "Low shadows, effects, foliage, reflections and lighting. Textures stay.",
            c => LowGroups(c.Game.SettingsFiles))
    };

    private static IReadOnlyList<Tweak> Pubg(AeoxContext ctx) => new[]
    {
        Make("pubg-fps", TweakCategory.Performance, "Uncapped frame rate", "No FPS limit in matches.",
            c => Ini(c.Game.SettingsFiles, Tsl, ("InGameFrameRateLimitType", "Unlimited"))),
        Make("pubg-vsync", TweakCategory.Performance, "Vsync off", "No waiting for the monitor, so less input delay.",
            c => Ini(c.Game.SettingsFiles, Tsl, ("bUseVSync", "False"))),
        Make("pubg-blur", TweakCategory.Visuals, "No motion blur", "Cleaner picture when turning.",
            c => Ini(c.Game.SettingsFiles, Tsl, ("bMotionBlur", "False")))
    };
}
