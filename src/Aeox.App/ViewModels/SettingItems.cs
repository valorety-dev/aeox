using System.Collections.ObjectModel;
using Aeox.Core.Changes;
using Aeox.Core.Tweaks;

namespace Aeox.App.ViewModels;

public interface ISettingItem
{
    string Title { get; }
    bool IsActive { get; }
    bool IsReverting { get; }
    IEnumerable<(string Source, IReadOnlyList<Change> Changes, bool Enabled)> Desired();
    void Resync(AeoxContext ctx, ChangeEngine engine);
}

public sealed class ChoiceOptionItem : Observable
{
    private readonly ChoiceItem _parent;
    private bool _isSelected;

    public ChoiceOptionItem(ChoiceItem parent, ChoiceOption option, int index)
    {
        _parent = parent;
        Option = option;
        Index = index;
    }

    public ChoiceOption Option { get; }
    public int Index { get; }
    public string Label => Option.Label;
    public string GroupName => _parent.GroupName;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!Set(ref _isSelected, value)) return;
            if (value) _parent.Select(Index);
        }
    }

    public void SetSilently(bool value)
    {
        _isSelected = value;
        Raise(nameof(IsSelected));
    }
}

public sealed class ChoiceItem : Observable, ISettingItem
{
    private readonly AeoxContext _ctx;
    private readonly Action _onChanged;
    private int _selected;
    private int _applied;

    public ChoiceItem(ChoiceSetting setting, AeoxContext ctx, ChangeEngine engine, Action onChanged)
    {
        Setting = setting;
        _ctx = ctx;
        _onChanged = onChanged;
        Options = new ObservableCollection<ChoiceOptionItem>(setting.Options.Select((o, i) => new ChoiceOptionItem(this, o, i)));
        _applied = _selected = setting.DetectSelected(ctx, engine);
        Options[_selected].SetSilently(true);
    }

    public ChoiceSetting Setting { get; }
    public ObservableCollection<ChoiceOptionItem> Options { get; }
    public string Title => Setting.Title;
    public string Description => Setting.Description;
    public string Glyph => Setting.Glyph;
    public string GroupName => "choice-" + Setting.Id;
    public bool IsActive => !Setting.Options[_applied].IsKeep;
    public ChoiceOption? ActiveOption => Setting.Options[_applied].IsKeep ? null : Setting.Options[_applied];
    public bool IsReverting => false;

    public void Select(int index)
    {
        if (_selected == index) return;
        _selected = index;
        foreach (var o in Options) if (o.Index != index) o.SetSilently(false);
        _onChanged();
    }

    public IEnumerable<(string Source, IReadOnlyList<Change> Changes, bool Enabled)> Desired()
    {
        var option = Setting.Options[_selected];
        if (option.IsKeep || _selected == _applied) yield break;
        yield return ($"{Title}: {option.Label}", option.Changes(_ctx), true);
    }

    public void Resync(AeoxContext ctx, ChangeEngine engine)
    {
        _applied = _selected = Setting.DetectSelected(ctx, engine);
        foreach (var o in Options) o.SetSilently(o.Index == _selected);
    }
}

public sealed class PageViewModel
{
    public PageViewModel(string key, string number, string title, string subtitle, IEnumerable<object> items)
    {
        Key = key;
        Number = number;
        Title = title;
        Subtitle = subtitle;
        Items = new ObservableCollection<object>(items);
    }

    public string Key { get; }
    public string Number { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public ObservableCollection<object> Items { get; }
    public bool IsEmpty => Items.Count == 0;
}
