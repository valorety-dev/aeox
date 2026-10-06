using Aeox.Core.Changes;
using Aeox.Core.Tweaks;

namespace Aeox.App.ViewModels;

public sealed class TweakItem : Observable
{
    private readonly Action _onChanged;
    private bool _isOn;
    private bool _isApplied;

    public TweakItem(Tweak tweak, AeoxContext ctx, bool isApplied, Action onChanged)
    {
        Tweak = tweak;
        Changes = tweak.Changes(ctx);
        _isApplied = isApplied;
        _isOn = isApplied;
        _onChanged = onChanged;
    }

    public Tweak Tweak { get; }
    public IReadOnlyList<Change> Changes { get; }
    public string Title => Tweak.Title;
    public string Description => Tweak.Description;
    public string Glyph => Tweak.Glyph;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (Set(ref _isOn, value)) _onChanged();
        }
    }

    public bool IsApplied
    {
        get => _isApplied;
        set => Set(ref _isApplied, value);
    }

    public void Sync(bool applied)
    {
        IsApplied = applied;
        _isOn = applied;
        Raise(nameof(IsOn));
    }
}
