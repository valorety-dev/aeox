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
        _isApplied = isApplied;
        _isOn = isApplied;
        _onChanged = onChanged;
    }

    public Tweak Tweak { get; }
    public IReadOnlyList<Change> Changes { get; }
    public string Title => Tweak.Title;
    public string Description => Tweak.Description;
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
        yield return (Title, Changes, IsOn);
    }

    public void Resync(AeoxContext ctx, ChangeEngine engine)
    {
        _isApplied = engine.IsApplied(Changes);
        _isOn = _isApplied;
        Raise(nameof(IsOn));
    }
}
