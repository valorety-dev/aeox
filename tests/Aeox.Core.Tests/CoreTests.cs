using Aeox.Core.Changes;
using Aeox.Core.Game;
using Aeox.Core.Hardware;
using Aeox.Core.Ini;
using Aeox.Core.Tweaks;

namespace Aeox.Core.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string _root;
    private readonly AeoxContext _ctx;

    public CoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aeox-tests-" + Guid.NewGuid().ToString("N"));
        var game = GameProfile.Retrac(Path.Combine(_root, "Saved"));
        var paths = game.Paths;
        Directory.CreateDirectory(paths.ConfigDir);
        File.WriteAllLines(paths.GameUserSettings, new[]
        {
            "[/Script/FortniteGame.FortGameUserSettings]",
            "FrameRateLimit=240.000000",
            "bShowGrass=True",
            "bUseVSync=True",
            "",
            "[ScalabilityGroups]",
            "sg.ResolutionQuality=100.000000",
            "sg.ShadowQuality=3"
        });
        _ctx = new AeoxContext(game, new HardwareInfo("AMD Ryzen 9 7900X3D", new[] { "NVIDIA GeForce RTX 4080 SUPER" }), Path.Combine(_root, "data"), "C:\\fake\\FortniteClient-Win64-Shipping.exe");
    }

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    private ChangeEngine NewEngine() => new(new OriginalStore(Path.Combine(_ctx.DataDir, "originals.json")));

    private IEnumerable<(string, IReadOnlyList<Change>, bool)> Desired(bool enabled) =>
        TweakCatalog.For(TweakCategory.Performance).Select(t => (t.Title, t.Changes(_ctx), enabled));

    [Fact]
    public void IniSetKeepsOtherLinesAndAddsMissingSection()
    {
        var doc = new IniDocument(new[] { "[A]", "x=1", "", "[B]", "y=2" });
        doc.Set("A", "x", "5");
        doc.Set("A", "z", "9");
        doc.Set("C", "k", "v");
        Assert.Equal(new[] { "[A]", "x=5", "z=9", "", "[B]", "y=2", "", "[C]", "k=v" }, doc.Lines);
        Assert.True(doc.Remove("B", "y"));
        Assert.Null(doc.Get("B", "y"));
    }

    [Fact]
    public void ApplyAllThenRestoreReturnsExactOriginalFiles()
    {
        var originalGus = File.ReadAllText(_ctx.Paths.GameUserSettings);
        var engine = NewEngine();

        engine.Apply(engine.Plan(Desired(true)));

        Assert.Equal("0.000000", IniDocument.Load(_ctx.Paths.GameUserSettings).Get(RetracGame.UserSettingsSection, "FrameRateLimit"));
        Assert.Equal("0", IniDocument.Load(_ctx.Paths.Engine).Get(RetracGame.ConsoleVariablesSection, "grass.Enable"));
        Assert.Equal("2", IniDocument.Load(_ctx.Paths.GameUserSettings).Get(RetracGame.UserSettingsSection, "LatencyTweak2"));
        Assert.True(new FileInfo(_ctx.Paths.GameUserSettings).IsReadOnly);
        Assert.All(TweakCatalog.For(TweakCategory.Performance), t => Assert.True(engine.IsApplied(t.Changes(_ctx)), t.Id));
        Assert.Empty(engine.Plan(Desired(true)));

        var reloaded = NewEngine();
        reloaded.Apply(reloaded.PlanRestoreAll());

        Assert.False(new FileInfo(_ctx.Paths.GameUserSettings).IsReadOnly);
        Assert.Equal(originalGus.Replace("\r\n", "\n").TrimEnd(), File.ReadAllText(_ctx.Paths.GameUserSettings).Replace("\r\n", "\n").TrimEnd());
        Assert.Null(IniDocument.Load(_ctx.Paths.Engine).Get(RetracGame.ConsoleVariablesSection, "grass.Enable"));
    }

    [Fact]
    public void DisablingOneTweakOnlyRevertsItsOwnKeys()
    {
        var engine = NewEngine();
        engine.Apply(engine.Plan(Desired(true)));

        var desired = TweakCatalog.For(TweakCategory.Performance).Select(t => (t.Title, t.Changes(_ctx), t.Id != "uncapped-fps"));
        var plan = engine.Plan(desired);

        Assert.Single(plan);
        Assert.Equal("FrameRateLimit", plan[0].Change.Key);
        Assert.Equal("240.000000", plan[0].NewValue);

        engine.Apply(plan);
        Assert.Equal("240.000000", IniDocument.Load(_ctx.Paths.GameUserSettings).Get(RetracGame.UserSettingsSection, "FrameRateLimit"));
        Assert.True(new FileInfo(_ctx.Paths.GameUserSettings).IsReadOnly);
    }

    [Fact]
    public void DisablingWithoutOriginalFallsBackToGameDefaults()
    {
        var engine = NewEngine();
        var grass = TweakCatalog.All.Single(t => t.Id == "no-grass");
        engine.Apply(engine.Plan(new[] { (grass.Title, grass.Changes(_ctx), true) }));
        engine.ForgetOriginals();

        var plan = engine.Plan(new[] { (grass.Title, grass.Changes(_ctx), false) });

        Assert.All(plan, p => Assert.Equal(RevertKind.GameDefault, p.Revert));
        Assert.Contains(plan, p => p.Change.Key == "bShowGrass" && p.NewValue == "True");
        Assert.Contains(plan, p => p.Change.Key == "grass.Enable" && p.NewValue is null);

        engine.Apply(plan);
        Assert.Null(IniDocument.Load(_ctx.Paths.Engine).Get(RetracGame.ConsoleVariablesSection, "grass.Enable"));
        Assert.Equal("True", IniDocument.Load(_ctx.Paths.GameUserSettings).Get(RetracGame.UserSettingsSection, "bShowGrass"));
    }

    [Fact]
    public void ChoiceDetectsAndAppliesSelectedOption()
    {
        var engine = NewEngine();
        var mode = TweakCatalog.Choices.Single(c => c.Id == "window-mode");
        Assert.Equal(0, mode.DetectSelected(_ctx, engine));

        var fullscreen = mode.Options.Single(o => o.Label == "Fullscreen");
        engine.Apply(engine.Plan(new[] { ("Window mode", fullscreen.Changes(_ctx), true) }));

        Assert.Equal("Fullscreen", mode.Options[mode.DetectSelected(_ctx, engine)].Label);
        Assert.Equal("0", IniDocument.Load(_ctx.Paths.GameUserSettings).Get(RetracGame.UserSettingsSection, "PreferredFullscreenMode"));

        var scale = TweakCatalog.Choices.Single(c => c.Id == "render-scale");
        var seventyFive = scale.Options.Single(o => o.Label == "75%");
        engine.Apply(engine.Plan(new[] { ("Render scale", seventyFive.Changes(_ctx), true) }));
        Assert.Equal("75.000000", IniDocument.Load(_ctx.Paths.GameUserSettings).Get(RetracGame.ScalabilitySection, "sg.ResolutionQuality"));
    }

    [Fact]
    public void LiveFortniteNeverTouchesEngineFiles()
    {
        var live = new AeoxContext(GameProfile.Fortnite(Path.Combine(_root, "Live")), _ctx.Hardware, _ctx.DataDir, "C:\\fake\\x.exe");
        var supported = TweakCatalog.All.Where(t => t.IsSupported(live) && t.Category != TweakCategory.System);
        var changes = supported.SelectMany(t => t.Changes(live)).ToList();
        Assert.NotEmpty(changes);
        Assert.DoesNotContain(changes, c => c.Target.EndsWith("Engine.ini") || c.Target.EndsWith("Input.ini"));
        Assert.DoesNotContain(supported, t => t.Id == "no-post-processing");
        Assert.Contains(TweakCatalog.Choices, c => c.Id == "renderer" && c.IsSupported(live));
        Assert.DoesNotContain(TweakCatalog.Choices, c => c.Id == "renderer" && c.IsSupported(_ctx));
        Assert.True(ChangeEngine.ValuesEqual("100", "100.000000"));
    }

    [Fact]
    public void PartlyMatchingTweakThatIsOffStaysUntouched()
    {
        var doc = IniDocument.Load(_ctx.Paths.GameUserSettings);
        doc.Set(RetracGame.ScalabilitySection, "sg.ShadowQuality", "0");
        doc.Save(_ctx.Paths.GameUserSettings);
        var engine = NewEngine();
        var world = TweakCatalog.All.Single(t => t.Id == "low-detail-world");
        Assert.False(engine.IsApplied(world.Changes(_ctx)));
        Assert.Empty(engine.Plan(new[] { (world.Title, world.Changes(_ctx), false) }));
    }

    [Fact]
    public void FrameCapDefaultFollowsMonitor()
    {
        Assert.Equal(240, new HardwareInfo("x", Array.Empty<string>(), 239).FrameCapForDisplay);
        Assert.Equal(144, new HardwareInfo("x", Array.Empty<string>(), 143).FrameCapForDisplay);
        Assert.Equal(60, new HardwareInfo("x", Array.Empty<string>(), 59).FrameCapForDisplay);
    }

    [Fact]
    public void ReflexIsOnlyAddedForNvidia()
    {
        var amdCtx = new AeoxContext(_ctx.Game, new HardwareInfo("Intel Core i7", new[] { "AMD Radeon RX 7800 XT" }), _ctx.DataDir);
        var latency = TweakCatalog.All.Single(t => t.Id == "low-latency");
        Assert.DoesNotContain(latency.Changes(amdCtx), c => c.Key == "LatencyTweak2");
        Assert.Contains(latency.Changes(_ctx), c => c.Key == "LatencyTweak2");
    }
}
