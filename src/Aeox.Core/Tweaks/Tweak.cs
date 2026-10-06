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
    public AeoxContext(RetracPaths paths, HardwareInfo hardware, string dataDir)
    {
        Paths = paths;
        Hardware = hardware;
        DataDir = dataDir;
    }

    public RetracPaths Paths { get; }
    public HardwareInfo Hardware { get; }
    public string DataDir { get; }

    public static string DefaultDataDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aeox");
}

public sealed class Tweak
{
    private readonly Func<AeoxContext, IReadOnlyList<Change>> _changes;
    private readonly Func<AeoxContext, bool> _supported;

    public Tweak(string id, TweakCategory category, string title, string description, string glyph,
        Func<AeoxContext, IReadOnlyList<Change>> changes, Func<AeoxContext, bool>? supported = null)
    {
        Id = id;
        Category = category;
        Title = title;
        Description = description;
        Glyph = glyph;
        _changes = changes;
        _supported = supported ?? (_ => true);
    }

    public string Id { get; }
    public TweakCategory Category { get; }
    public string Title { get; }
    public string Description { get; }
    public string Glyph { get; }

    public IReadOnlyList<Change> Changes(AeoxContext ctx) => _changes(ctx);

    public bool IsSupported(AeoxContext ctx) => _supported(ctx);
}
