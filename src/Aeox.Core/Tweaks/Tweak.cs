using Aeox.Core.Changes;
using Aeox.Core.Game;
using Aeox.Core.Hardware;

namespace Aeox.Core.Tweaks;

public enum TweakCategory
{
    Performance,
    Visuals,
    Network,
    System
}

public sealed class AeoxContext
{
    public AeoxContext(GameProfile game, HardwareInfo hardware, string dataDir, string? gameExe = null)
    {
        Game = game;
        Hardware = hardware;
        DataDir = dataDir;
        GameExe = gameExe ?? game.FindExe();
    }

    public GameProfile Game { get; }
    public string? GameExe { get; }

    public GamePaths Paths => Game.Paths;
    public HardwareInfo Hardware { get; }
    public string DataDir { get; }

    public static string DefaultDataDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aeox");
}

public sealed class Tweak
{
    private readonly Func<AeoxContext, IReadOnlyList<Change>> _changes;
    private readonly Func<AeoxContext, bool> _supported;
    private readonly Func<AeoxContext, bool> _recommended;
    private readonly Func<AeoxContext, string?> _tag;
    private readonly Func<AeoxContext, string>? _describe;

    public Tweak(string id, TweakCategory category, string title, string description, string glyph,
        Func<AeoxContext, IReadOnlyList<Change>> changes, Func<AeoxContext, bool>? supported = null,
        Func<AeoxContext, bool>? recommended = null, Func<AeoxContext, string?>? tag = null, Func<AeoxContext, string>? describe = null)
    {
        Id = id;
        Category = category;
        Title = title;
        Description = description;
        Glyph = glyph;
        _changes = changes;
        _supported = supported ?? (_ => true);
        _recommended = recommended ?? (_ => true);
        _tag = tag ?? (_ => null);
        _describe = describe;
    }

    public string Id { get; }
    public TweakCategory Category { get; }
    public string Title { get; }
    public string Description { get; }
    public string Glyph { get; }

    public IReadOnlyList<Change> Changes(AeoxContext ctx) => _changes(ctx);

    public bool IsSupported(AeoxContext ctx) => _supported(ctx);

    public bool IsRecommended(AeoxContext ctx) => _recommended(ctx);

    public string? Tag(AeoxContext ctx) => _tag(ctx);

    public string Describe(AeoxContext ctx) => _describe?.Invoke(ctx) ?? Description;
}
