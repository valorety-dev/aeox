using System.Text.Json;

namespace Aeox.Core.Changes;

public sealed record OriginalEntry(ChangeKind Kind, string Target, string Section, string Key, string? Value)
{
    public Change ToChange() => new(Kind, Target, Section, Key, Value);
}

public sealed class OriginalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Dictionary<string, OriginalEntry> _entries;

    public OriginalStore(string path)
    {
        _path = path;
        _entries = new Dictionary<string, OriginalEntry>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return;
        var list = JsonSerializer.Deserialize<List<OriginalEntry>>(File.ReadAllText(path)) ?? new List<OriginalEntry>();
        foreach (var e in list) _entries[e.ToChange().Id] = e;
    }

    public IReadOnlyCollection<OriginalEntry> All => _entries.Values;

    public bool Has(Change change) => _entries.ContainsKey(change.Id);

    public bool TryGet(Change change, out string? value)
    {
        if (_entries.TryGetValue(change.Id, out var e))
        {
            value = e.Value;
            return true;
        }
        value = null;
        return false;
    }

    public void Remember(Change change, string? currentValue)
    {
        if (_entries.ContainsKey(change.Id)) return;
        _entries[change.Id] = new OriginalEntry(change.Kind, change.Target, change.Section, change.Key, currentValue);
    }

    public void Clear() => _entries.Clear();

    public void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_entries.Values.ToList(), JsonOptions));
        File.Move(tmp, _path, true);
    }
}
