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
        var paths = new RetracPaths(Path.Combine(_root, "Saved"));
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
        _ctx = new AeoxContext(paths, new HardwareInfo("AMD Ryzen 9 7900X3D", new[] { "NVIDIA GeForce RTX 4080 SUPER" }), Path.Combine(_root, "data"));
    }

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, true);
    }

    private ChangeEngine NewEngine() => new(new OriginalStore(Path.Combine(_ctx.DataDir, "originals.json")));

    private IEnumerable<(string, IReadOnlyList<Change>, bool)> Desired(bool enabled) =>
        TweakCatalog.All.Select(t => (t.Title, t.Changes(_ctx), enabled));

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
        Assert.All(TweakCatalog.All, t => Assert.True(engine.IsApplied(t.Changes(_ctx)), t.Id));
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

        var desired = TweakCatalog.All.Select(t => (t.Title, t.Changes(_ctx), t.Id != "uncapped-fps"));
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
    public void FrameCapDefaultFollowsMonitor()
    {
        Assert.Equal(240, new HardwareInfo("x", Array.Empty<string>(), 239).FrameCapForDisplay);
        Assert.Equal(144, new HardwareInfo("x", Array.Empty<string>(), 143).FrameCapForDisplay);
        Assert.Equal(60, new HardwareInfo("x", Array.Empty<string>(), 59).FrameCapForDisplay);
    }

    [Fact]
    public void ReflexIsOnlyAddedForNvidia()
    {
        var amdCtx = new AeoxContext(_ctx.Paths, new HardwareInfo("Intel Core i7", new[] { "AMD Radeon RX 7800 XT" }), _ctx.DataDir);
        var latency = TweakCatalog.All.Single(t => t.Id == "low-latency");
        Assert.DoesNotContain(latency.Changes(amdCtx), c => c.Key == "LatencyTweak2");
        Assert.Contains(latency.Changes(_ctx), c => c.Key == "LatencyTweak2");
    }
}
