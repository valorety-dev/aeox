using Aeox.Core.Changes;
using Aeox.Core.Tweaks;

namespace Aeox.App.ViewModels;

public sealed class TweakItem : Observable, ISettingItem
{
    private readonly Action _onChanged;
    private bool _isOn;
    private bool _isApplied;

    public TweakItem(Tweak tweak, AeoxContext ctx, bool isApplied, Action onChanged)
    {
        Tweak = tweak;
        Changes = tweak.Changes(ctx);
        Description = tweak.Describe(ctx);
        IsRecommended = tweak.IsRecommended(ctx);
        var parts = new List<string>();
        if (tweak.Tag(ctx) is { } tag) parts.Add(tag);
        if (Changes.Any(c => c.NeedsAdmin)) parts.Add("asks for admin");
        TagText = string.Join("  ·  ", parts);
        _isApplied = isApplied;
        _isOn = isApplied;
        _onChanged = onChanged;
    }

    public Tweak Tweak { get; }
    public IReadOnlyList<Change> Changes { get; }
    public string Title => Tweak.Title;
    public string Description { get; }
    public string TagText { get; }
    public bool HasTag => TagText.Length > 0;
    public bool IsRecommended { get; }
    public bool NeedsAdmin => Changes.Any(c => c.NeedsAdmin);
    public string Glyph => Tweak.Glyph;
    public bool IsActive => _isApplied;
    public bool IsReverting => !_isOn && _isApplied;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (Set(ref _isOn, value)) _onChanged();
        }
    }

    public IEnumerable<(string Source, IReadOnlyList<Change> Changes, bool Enabled)> Desired()
    {
        if (!IsOn && !_isApplied) yield break;
        yield return (Title, Changes, IsOn);
    }

    public void Resync(AeoxContext ctx, ChangeEngine engine)
    {
        _isApplied = engine.IsApplied(Changes);
        _isOn = _isApplied;
        Raise(nameof(IsOn));
    }
}
