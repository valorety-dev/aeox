using Aeox.Core.Changes;
using Aeox.Core.Game;

namespace Aeox.Core.Tweaks;

public static class TweakCatalog
{
    private const string Gus = RetracGame.UserSettingsSection;
    private const string Cvars = RetracGame.ConsoleVariablesSection;
    private const string Scalability = RetracGame.ScalabilitySection;

    private static readonly string[] LowScalabilityGroups =
    {
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.PostProcessQuality",
        "sg.TextureQuality", "sg.EffectsQuality", "sg.FoliageQuality", "sg.ShadingQuality"
    };

    public static IReadOnlyList<Tweak> All { get; } = new List<Tweak>
    {
        new("uncapped-fps", TweakCategory.Performance,
            "Uncapped frame rate",
            "Removes the in-game FPS cap so your PC can render as fast as it can.",
            "\uEC4A",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.GameUserSettings, Gus, "FrameRateLimit", "0.000000", $"{ctx.Hardware.FrameCapForDisplay}.000000")
            }),

        new("low-latency", TweakCategory.Performance,
            "Improve input latency",
            "Turns off vsync and mouse smoothing, and enables NVIDIA Reflex On + Boost when available.",
            "\uE962",
            ctx =>
            {
                var list = new List<Change>
                {
                    Change.Ini(ctx.Paths.GameUserSettings, Gus, "bUseVSync", "False", "False"),
                    Change.Ini(ctx.Paths.Engine, Cvars, "r.VSync", "0"),
                    Change.Ini(ctx.Paths.Input, RetracGame.InputSection, "bEnableMouseSmoothing", "False"),
                    Change.Ini(ctx.Paths.Input, RetracGame.InputSection, "bViewAccelerationEnabled", "False")
                };
                if (ctx.Hardware.HasNvidia) list.Add(Change.Ini(ctx.Paths.GameUserSettings, Gus, "LatencyTweak2", "2", "0"));
                return list;
            }),

        new("low-detail-world", TweakCategory.Performance,
            "Lightweight world",
            "Low quality presets and the simplest tree and building models. Cover stays exactly where it is.",
            "\uF158",
            ctx =>
            {
                var list = LowScalabilityGroups.Select(g => Change.Ini(ctx.Paths.GameUserSettings, Scalability, g, "0", "2")).ToList();
                list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "foliage.LODDistanceScale", "0.1"));
                list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "foliage.DitheredLOD", "0"));
                list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "r.StaticMeshLODDistanceScale", "3"));
                list.Add(Change.Ini(ctx.Paths.Engine, Cvars, "r.MaxAnisotropy", "0"));
                return list;
            }),

        new("no-post-processing", TweakCategory.Performance,
            "Remove post-processing",
            "Disables motion blur, bloom, depth of field, ambient occlusion, lens flares and film grain.",
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
            }),

        new("no-grass", TweakCategory.Performance,
            "Disable grass",
            "Stops grass from being drawn at all, which saves GPU and CPU time in every frame.",
            "\uEC0A",
            ctx => new[]
            {
                Change.Ini(ctx.Paths.GameUserSettings, Gus, "bShowGrass", "False", "True"),
                Change.Ini(ctx.Paths.Engine, Cvars, "grass.Enable", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "grass.DensityScale", "0"),
                Change.Ini(ctx.Paths.Engine, Cvars, "grass.CullDistanceScale", "0")
            }),

        new("lock-settings", TweakCategory.Performance,
            "Lock settings",
            "Makes the config files read-only so the game or launcher cannot quietly reset them.",
            "\uE72E",
            ctx => new[]
            {
                Change.ReadOnly(ctx.Paths.GameUserSettings, true),
                Change.ReadOnly(ctx.Paths.Engine, true),
                Change.ReadOnly(ctx.Paths.Input, true)
            })
    };

    public static IEnumerable<Tweak> For(TweakCategory category) => All.Where(t => t.Category == category);
}
