using Aeox.Core.Changes;

namespace Aeox.Core.Tweaks;

public sealed record ChoiceOption(string Label, Func<AeoxContext, IReadOnlyList<Change>> Changes)
{
    public static ChoiceOption Keep { get; } = new("Keep", _ => Array.Empty<Change>());

    public bool IsKeep => ReferenceEquals(this, Keep);
}

public sealed class ChoiceSetting
{
    public ChoiceSetting(string id, TweakCategory category, string title, string description, string glyph,
        IReadOnlyList<ChoiceOption> options)
    {
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
