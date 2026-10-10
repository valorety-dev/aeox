using Aeox.Core.Changes;

namespace Aeox.Core.Tweaks;

public sealed record ChoiceOption(string Label, Func<AeoxContext, IReadOnlyList<Change>> Changes)
{
    public static ChoiceOption Keep { get; } = new("Keep", _ => Array.Empty<Change>());

    public bool IsKeep => ReferenceEquals(this, Keep);
}

public sealed class ChoiceSetting
{
    private readonly Func<AeoxContext, bool> _supported;
    private readonly Func<AeoxContext, string?> _recommended;

    public ChoiceSetting(string id, TweakCategory category, string title, string description, string glyph,
        IReadOnlyList<ChoiceOption> options, Func<AeoxContext, bool>? supported = null, Func<AeoxContext, string?>? recommended = null)
    {
        _supported = supported ?? (_ => true);
        _recommended = recommended ?? (_ => null);
        Id = id;
        Category = category;
        Title = title;
        Description = description;
        Glyph = glyph;
        Options = options;
    }

    public string Id { get; }
    public TweakCategory Category { get; }
    public string Title { get; }
    public string Description { get; }
    public string Glyph { get; }
    public IReadOnlyList<ChoiceOption> Options { get; }

    public bool IsSupported(AeoxContext ctx) => _supported(ctx);

    public int? RecommendedIndex(AeoxContext ctx)
    {
        var label = _recommended(ctx);
        if (label is null) return null;
        var i = Options.ToList().FindIndex(o => o.Label == label);
        return i >= 0 ? i : null;
    }

    public int DetectSelected(AeoxContext ctx, ChangeEngine engine)
    {
        for (var i = 0; i < Options.Count; i++)
        {
            var changes = Options[i].Changes(ctx);
            if (changes.Count > 0 && engine.IsApplied(changes)) return i;
        }
        var keep = Options.ToList().FindIndex(o => o.IsKeep);
        return keep >= 0 ? keep : 0;
    }
}
