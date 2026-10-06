using Aeox.Core.Changes;
using Aeox.Core.Game;

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
            }),

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
                if (ctx.Hardware.HasNvidia) list.Add(Change.Ini(ctx.Paths.GameUserSettings, Gus, "LatencyTweak2", "2", "0"));
                return list;
            }),

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
                : new[] { Change.ReadOnly(ctx.Paths.GameUserSettings, true) }),

        new("show-fps", TweakCategory.Visuals,
            "FPS counter",
            "Shows your frame rate in the corner while playing.",
            "\uE9D9",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.GameUserSettings, Gus, "bShowFPS", "True", "False")
            }),

        new("dedicated-gpu", TweakCategory.System,
            "Use the dedicated GPU",
            "Fortnite always runs on your graphics card.",
            "\uE950",
            ctx => new[]
            {
                Change.Registry(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", ctx.GameExe!, "sz:GpuPreference=2;")
            },
            ctx => ctx.GameExe is not null && ctx.Hardware.HasMultipleGpus),

        new("no-fso", TweakCategory.System,
            "Disable fullscreen optimizations",
            "True exclusive fullscreen for Fortnite.",
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
            }),

        new("no-mouse-acceleration", TweakCategory.System,
            "Raw mouse movement",
            "Turns off Windows pointer acceleration so aim is consistent.",
            "\uE962",
            _ => new[]
            {
                Change.Registry(Aeox.Core.Windows.MouseSettings.RegistryPath, "MouseSpeed", "sz:0", "sz:1"),
                Change.Registry(Aeox.Core.Windows.MouseSettings.RegistryPath, "MouseThreshold1", "sz:0", "sz:6"),
                Change.Registry(Aeox.Core.Windows.MouseSettings.RegistryPath, "MouseThreshold2", "sz:0", "sz:10")
            }),

        new("power-plan", TweakCategory.System,
            "Best power plan",
            "Balanced on X3D chips, High performance otherwise.",
            "\uE945",
            ctx => new[]
            {
                Change.PowerPlan(ctx.Hardware.IsX3D ? Aeox.Core.Windows.PowerPlans.Balanced : Aeox.Core.Windows.PowerPlans.HighPerformance,
                    Aeox.Core.Windows.PowerPlans.Balanced)
            })
    };

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
            ctx => ctx.Game.HasPerformanceMode),

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
            })
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
