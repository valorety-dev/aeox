using Aeox.Core.Changes;
using Aeox.Core.Game;
using Aeox.Core.Hardware;
using Aeox.Core.Network;
using Aeox.Core.Windows;

namespace Aeox.Core.Tweaks;

public static class TweakCatalog
{
    private const string Gus = RetracGame.UserSettingsSection;
    private const string Cvars = RetracGame.ConsoleVariablesSection;
    private const string Scalability = RetracGame.ScalabilitySection;

    private static readonly string[] LiveOnlyScalabilityGroups =
    {
        "sg.GlobalIlluminationQuality", "sg.ReflectionQuality", "sg.LandscapeQuality"
    };

    private const string NetPlayer = "/Script/Engine.Player";
    private const string NetDriver = "/Script/OnlineSubsystemUtils.IpNetDriver";
    private const string Multimedia = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string VCachePrefs = @"HKLM\SYSTEM\CurrentControlSet\Services\amd3dvcache\Preferences";

    private static readonly string[] LowScalabilityGroups =
    {
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.PostProcessQuality",
        "sg.TextureQuality", "sg.EffectsQuality", "sg.FoliageQuality", "sg.ShadingQuality"
    };

    public static IReadOnlyList<Tweak> All { get; } = new List<Tweak>
    {
        new("uncapped-fps", TweakCategory.Performance,
            "Uncapped frame rate",
            "No FPS limit.",
            "\uEC4A",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.GameUserSettings, Gus, "FrameRateLimit", "0.000000", $"{ctx.Hardware.FrameCapForDisplay}.000000")
            },
            recommended: ctx => !ctx.Hardware.IsLaptop,
            describe: ctx => ctx.Hardware.IsLaptop
                ? "No FPS limit. On a laptop this means more heat and fan noise."
                : "No FPS limit."),

        new("low-latency", TweakCategory.Performance,
            "Improve input latency",
            "Vsync off, raw mouse, Reflex + Boost on NVIDIA.",
            "\uE962",
            ctx =>
            {
                var list = new List<Change>
                {
                    Change.Ini(ctx.Paths.GameUserSettings, Gus, "bUseVSync", "False", "False")
                };
                if (ctx.Game.SupportsEngineTweaks)
                {
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "r.VSync", "0"));
                    list.Add(Change.Ini(ctx.Paths.Input, RetracGame.InputSection, "bEnableMouseSmoothing", "False"));
                    list.Add(Change.Ini(ctx.Paths.Input, RetracGame.InputSection, "bViewAccelerationEnabled", "False"));
                }
                else
                {
                    list.Add(Change.Ini(ctx.Paths.GameUserSettings, Gus, "bDisableMouseAcceleration", "True", "False"));
                }
                if (ctx.Hardware.HasNvidia && ctx.Game.SupportsReflex) list.Add(Change.Ini(ctx.Paths.GameUserSettings, Gus, "LatencyTweak2", ctx.Hardware.IsLaptop ? "1" : "2", "0"));
                return list;
            },
            tag: ctx => ctx.Hardware.HasNvidia && ctx.Game.SupportsReflex ? "reflex · nvidia" : null,
            describe: ctx => !ctx.Game.SupportsReflex
                ? "Vsync off and raw mouse. This build is older than NVIDIA Reflex."
                : ctx.Hardware.HasNvidia
                ? ctx.Hardware.IsLaptop ? "Vsync off, raw mouse and NVIDIA Reflex. Boost stays off to keep the laptop cool." : "Vsync off, raw mouse, NVIDIA Reflex + Boost."
                : "Vsync off and raw mouse. Reflex needs an NVIDIA card, so it is skipped."),

        new("low-detail-world", TweakCategory.Performance,
            "Lightweight world",
            "Simplest models and presets. Cover stays the same.",
            "\uF158",
            ctx =>
            {
                var groups = ctx.Game.SupportsEngineTweaks ? LowScalabilityGroups : LowScalabilityGroups.Concat(LiveOnlyScalabilityGroups);
                var list = groups.Select(g => Change.Ini(ctx.Paths.GameUserSettings, Scalability, g, "0", "2")).ToList();
                if (ctx.Game.SupportsEngineTweaks)
                {
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "foliage.LODDistanceScale", "0.1"));
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "foliage.DitheredLOD", "0"));
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "r.StaticMeshLODDistanceScale", "3"));
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "r.MaxAnisotropy", "0"));
                }
                else
                {
                    list.Add(Change.Ini(ctx.Paths.GameUserSettings, Gus, "bRayTracing", "False", "False"));
                }
                return list;
            }),

        new("no-post-processing", TweakCategory.Performance,
            "Remove post-processing",
            "No blur, bloom, depth of field or grain.",
            "\uE794",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.GameUserSettings, Gus, "bMotionBlur", "False", "True"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.MotionBlurQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.DefaultFeature.MotionBlur", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.DepthOfFieldQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.BloomQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.DefaultFeature.Bloom", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.LensFlareQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.SceneColorFringeQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.LightShaftQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.AmbientOcclusionLevels", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.DefaultFeature.AmbientOcclusion", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.SSR.Quality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.RefractionQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.ParticleLightQuality", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "r.Tonemapper.GrainQuantization", "0")
            },
            ctx => ctx.Game.SupportsEngineTweaks),

        new("no-grass", TweakCategory.Performance,
            "Disable grass",
            "Grass is not drawn at all.",
            "\uEC0A",
            ctx =>
            {
                var list = new List<Change> { Change.Ini(ctx.Paths.GameUserSettings, Gus, "bShowGrass", "False", "True") };
                if (ctx.Game.SupportsEngineTweaks)
                {
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "grass.Enable", "0"));
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "grass.DensityScale", "0"));
                    list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "grass.CullDistanceScale", "0"));
                }
                return list;
            }),

        new("lock-settings", TweakCategory.Performance,
            "Lock settings",
            "Stops the game from resetting your settings.",
            "\uE72E",
            ctx => ctx.Game.SupportsEngineTweaks
                ? new[]
                {
                    Change.ReadOnly(ctx.Paths.GameUserSettings, true),
                    Change.ReadOnly(ctx.Paths.Engine, true),
                    Change.ReadOnly(ctx.Paths.Input, true)
                }
                : new[] { Change.ReadOnly(ctx.Paths.GameUserSettings, true) },
            recommended: _ => false),

        new("show-fps", TweakCategory.Visuals,
            "FPS counter",
            "Shows your frame rate in the corner while playing.",
            "\uE9D9",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.GameUserSettings, Gus, "bShowFPS", "True", "False")
            },
            recommended: _ => false),

        new("dedicated-gpu", TweakCategory.System,
            "Use the dedicated GPU",
            "Fortnite always runs on your graphics card.",
            "\uE950",
            ctx => new[]
            {
                Change.Registry(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", ctx.GameExe!, "sz:GpuPreference=2;")
            },
            ctx => ctx.GameExe is not null && ctx.Hardware.HasMultipleGpus,
            tag: _ => "2 gpus",
            describe: ctx => $"{ctx.Game.ShortName} always runs on your graphics card, never the built-in graphics."),

        new("no-fso", TweakCategory.System,
            "Disable fullscreen optimizations",
            "True exclusive fullscreen for the game.",
            "\uE8A7",
            ctx => new[]
            {
                Change.Registry(@"HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", ctx.GameExe!, "sz:~ DISABLEDXMAXIMIZEDWINDOWEDMODE")
            },
            ctx => ctx.GameExe is not null),

        new("game-mode", TweakCategory.System,
            "Game Mode",
            "Windows prioritises the game and pauses updates while you play.",
            "\uE7FC",
            _ => new[]
            {
                Change.Registry(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", "dword:1"),
                Change.Registry(@"HKCU\Software\Microsoft\GameBar", "AllowAutoGameMode", "dword:1")
            }),

        new("no-background-recording", TweakCategory.System,
            "No background recording",
            "Stops Game Bar from recording gameplay in the background.",
            "\uE714",
            _ => new[]
            {
                Change.Registry(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", "dword:0", "dword:1"),
                Change.Registry(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", "dword:0")
            },
            recommended: ctx => !ctx.Hardware.IsDualCcdX3D,
            describe: ctx => ctx.Hardware.IsDualCcdX3D
                ? "Stops background clips. Not picked for your X3D chip: AMD uses Game Bar to spot games."
                : "Stops Game Bar from recording gameplay in the background."),

        new("no-mouse-acceleration", TweakCategory.System,
            "Raw mouse movement",
            "Turns off Windows pointer acceleration so aim is consistent.",
            "\uE962",
            _ => new[]
            {
                Change.Registry(MouseSettings.RegistryPath, "MouseSpeed", "sz:0", "sz:1"),
                Change.Registry(MouseSettings.RegistryPath, "MouseThreshold1", "sz:0", "sz:6"),
                Change.Registry(MouseSettings.RegistryPath, "MouseThreshold2", "sz:0", "sz:10")
            }),

        new("power-plan", TweakCategory.System,
            "Best power plan",
            "Balanced on X3D chips and laptops, High performance otherwise.",
            "\uE945",
            ctx => new[]
            {
                Change.PowerPlan(WantsBalanced(ctx.Hardware) ? PowerPlans.Balanced : PowerPlans.HighPerformance, PowerPlans.Balanced)
            },
            tag: ctx => ctx.Hardware.IsX3D ? "x3d" : ctx.Hardware.IsLaptop ? "laptop" : null,
            describe: ctx => ctx.Hardware.IsX3D ? "Balanced, so AMD can move games onto the V-Cache cores."
                : ctx.Hardware.IsLaptop ? "Balanced, so the laptop boosts when needed without overheating."
                : "High performance, so the CPU never drops into power saving mid-fight."),

        new("vcache-first", TweakCategory.System,
            "Games on V-Cache cores",
            "Windows tries the 3D V-Cache cores first, like a single-CCD X3D. Active after a restart.",
            "\uE950",
            _ => new[]
            {
                Change.Registry(VCachePrefs, "DefaultType", "dword:1", "dword:0")
            },
            ctx => ctx.Hardware.IsDualCcdX3D && Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\amd3dvcache") is not null,
            tag: _ => "x3d · 2 ccds"),

        new("net-rate", TweakCategory.Network,
            "Higher game bandwidth",
            "Lifts the game's own bandwidth cap so busy fights are not throttled on your side.",
            "\uE839",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.Engine, NetPlayer, "ConfiguredInternetSpeed", "100000"),
                Change.Ini(ctx.Paths.Engine, NetPlayer, "ConfiguredLanSpeed", "100000"),
                Change.Ini(ctx.Paths.Engine, NetDriver, "MaxClientRate", "100000"),
                Change.Ini(ctx.Paths.Engine, NetDriver, "MaxInternetClientRate", "100000")
            },
            ctx => ctx.Game.SupportsEngineTweaks),

        new("no-net-throttling", TweakCategory.Network,
            "No network throttling",
            "Windows slows game packets while music or Discord audio plays. This turns that off. Active after a restart.",
            "\uE9E9",
            _ => new[]
            {
                Change.Registry(Multimedia, "NetworkThrottlingIndex", "dword:-1", "dword:10")
            }),

        new("adapter-power", TweakCategory.Network,
            "Adapter power saving off",
            "Your network adapter stays awake between packets. Reconnects for a few seconds when applied.",
            "\uE83E",
            ctx => AdapterChanges(ctx, NetworkAdapters.PowerSaving, true),
            ctx => ctx.Hardware.Adapter is not null,
            recommended: ctx => !ctx.Hardware.IsLaptop,
            tag: ctx => AdapterTag(ctx),
            describe: ctx => $"Your {(ctx.Hardware.IsWireless ? "Wi-Fi card" : "network card")} stays awake between packets. Reconnects for a few seconds when applied."),

        new("adapter-batching", TweakCategory.Network,
            "Interrupt moderation off",
            "Packets reach the game right away instead of in batches. Costs a little CPU. Reconnects for a few seconds.",
            "\uE8AB",
            ctx => AdapterChanges(ctx, NetworkAdapters.Batching, false),
            ctx => AdapterChanges(ctx, NetworkAdapters.Batching, false).Count > 0,
            recommended: ctx => ctx.Hardware.Threads >= 12 && !ctx.Hardware.IsLaptop,
            tag: ctx => AdapterTag(ctx)),

        new("wifi-roaming", TweakCategory.Network,
            "Steady Wi-Fi",
            "Stops the adapter hunting for other access points mid-match, a common cause of lag spikes every minute or so.",
            "\uE701",
            ctx => AdapterChanges(ctx, NetworkAdapters.Roaming, false),
            ctx => ctx.Hardware.IsWireless && AdapterChanges(ctx, NetworkAdapters.Roaming, false).Count > 0,
            recommended: ctx => !ctx.Hardware.IsLaptop,
            tag: _ => "wi-fi")
    };

    private static bool WantsBalanced(HardwareInfo hw) => hw.IsX3D || hw.IsLaptop;

    private static string? AdapterTag(AeoxContext ctx) => ctx.Hardware.Adapter is { } a ? (a.IsWireless ? "wi-fi" : "ethernet") : null;

    private static IReadOnlyList<Change> AdapterChanges(AeoxContext ctx, IEnumerable<(string Keyword, string Value)> wanted, bool includePowerOff)
    {
        if (ctx.Hardware.Adapter is not { } adapter) return Array.Empty<Change>();
        var list = NetworkAdapters.Pick(adapter, wanted).ToList();
        if (includePowerOff) list.Add(NetworkAdapters.AllowPowerOff(adapter, false));
        return list;
    }

    public static IReadOnlyList<ChoiceSetting> Choices { get; } = new List<ChoiceSetting>
    {
        new("renderer", TweakCategory.Visuals,
            "Rendering mode",
            "Performance mode is the lightweight renderer behind 400+ FPS.",
            "\uE945",
            new[]
            {
                ChoiceOption.Keep,
                Renderer("Performance", "dx11", "es31"),
                Renderer("DirectX 11", "dx11", "sm5"),
                Renderer("DirectX 12", "dx12", "sm6")
            },
            ctx => ctx.Game.HasPerformanceMode,
            ctx => ctx.Hardware.Tier == GpuTier.Entry ? "Performance" : null),

        new("window-mode", TweakCategory.Visuals,
            "Window mode",
            "Fullscreen has the lowest input delay.",
            "\uE740",
            new[]
            {
                ChoiceOption.Keep,
                WindowMode("Fullscreen", 0),
                WindowMode("Borderless", 1),
                WindowMode("Windowed", 2)
            }),

        new("resolution", TweakCategory.Visuals,
            "Resolution",
            "Stretched 4:3 needs Fullscreen and GPU scaling set to full-screen.",
            "\uE7F4",
            new[]
            {
                ChoiceOption.Keep,
                new ChoiceOption("Native", ctx => Resolution(ctx, ctx.Hardware.ScreenWidth, ctx.Hardware.ScreenHeight)),
                new ChoiceOption("1680×1050", ctx => Resolution(ctx, 1680, 1050)),
                new ChoiceOption("1440×1080", ctx => Resolution(ctx, 1440, 1080)),
                new ChoiceOption("1280×960", ctx => Resolution(ctx, 1280, 960))
            }),

        new("render-scale", TweakCategory.Visuals,
            "Render scale",
            "Lower is faster and softer. 100% keeps it sharp.",
            "\uE71E",
            new[]
            {
                ChoiceOption.Keep,
                RenderScale(100),
                RenderScale(85),
                RenderScale(75),
                RenderScale(50)
            },
            recommended: ctx => ctx.Hardware.Tier == GpuTier.Entry ? "75%" : null)
    };

    public static IEnumerable<Tweak> For(TweakCategory category) => All.Where(t => t.Category == category);

    public static IEnumerable<ChoiceSetting> ChoicesFor(TweakCategory category) => Choices.Where(c => c.Category == category);

    private static ChoiceOption WindowMode(string label, int mode) =>
        new(label, ctx => new[]
        {
            Change.Ini(ctx.Paths.GameUserSettings, Gus, "FullscreenMode", mode.ToString()),
            Change.Ini(ctx.Paths.GameUserSettings, Gus, "LastConfirmedFullscreenMode", mode.ToString()),
            Change.Ini(ctx.Paths.GameUserSettings, Gus, "PreferredFullscreenMode", mode.ToString())
        });

    private static IReadOnlyList<Change> Resolution(AeoxContext ctx, int w, int h) => new[]
    {
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "ResolutionSizeX", w.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "ResolutionSizeY", h.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "LastUserConfirmedResolutionSizeX", w.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "LastUserConfirmedResolutionSizeY", h.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "DesiredScreenWidth", w.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "DesiredScreenHeight", h.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "LastUserConfirmedDesiredScreenWidth", w.ToString()),
        Change.Ini(ctx.Paths.GameUserSettings, Gus, "LastUserConfirmedDesiredScreenHeight", h.ToString())
    };

    private static ChoiceOption Renderer(string label, string rhi, string featureLevel) =>
        new(label, ctx => new[]
        {
            Change.Ini(ctx.Paths.GameUserSettings, RetracGame.RhiSection, "PreferredRHI", rhi),
            Change.Ini(ctx.Paths.GameUserSettings, RetracGame.RhiSection, "PreferredFeatureLevel", featureLevel)
        });

    private static ChoiceOption RenderScale(int percent) =>
        new($"{percent}%", ctx => new[]
        {
            Change.Ini(ctx.Paths.GameUserSettings, Scalability, "sg.ResolutionQuality", $"{percent}.000000")
        });
}
