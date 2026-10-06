using Aeox.Core.Ini;
using Microsoft.Win32;

namespace Aeox.Core.Changes;

public sealed class ChangeEngine
{
    private readonly OriginalStore _store;

    public ChangeEngine(OriginalStore store)
    {
        _store = store;
    }

    public OriginalStore Store => _store;

    public string? ReadCurrent(Change change) => change.Kind switch
    {
        ChangeKind.Ini => IniDocument.Load(change.Target).Get(change.Section, change.Key),
        ChangeKind.FileReadOnly => File.Exists(change.Target)
            ? (new FileInfo(change.Target).IsReadOnly ? "true" : "false")
            : null,
        ChangeKind.Registry => ReadRegistry(change.Target, change.Key),
        _ => null
    };

    public bool IsApplied(IEnumerable<Change> changes) =>
        changes.All(c => ValuesEqual(ReadCurrent(c), c.Value));

    public IReadOnlyList<PlannedChange> Plan(IEnumerable<(string Source, IReadOnlyList<Change> Changes, bool Enabled)> desired)
    {
        var plan = new List<PlannedChange>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, changes, enabled) in desired)
        {
            foreach (var change in changes)
            {
                if (!seen.Add(change.Id)) continue;
                var current = ReadCurrent(change);
                string? target;
                var revert = RevertKind.None;
                if (enabled)
                {
                    target = change.Value;
                }
                else if (_store.TryGet(change, out var original) && !ValuesEqual(original, change.Value))
                {
                    target = original;
                    revert = RevertKind.Original;
                }
                else
                {
                    if (!ValuesEqual(current, change.Value)) continue;
                    target = change.Default;
                    revert = RevertKind.GameDefault;
                }
                if (!ValuesEqual(current, target)) plan.Add(new PlannedChange(change, current, target, source, revert));
            }
        }
        return plan;
    }

    public IReadOnlyList<PlannedChange> PlanRestoreAll()
    {
        var plan = new List<PlannedChange>();
        foreach (var entry in _store.All)
        {
            var change = entry.ToChange();
            var current = ReadCurrent(change);
            if (!ValuesEqual(current, entry.Value)) plan.Add(new PlannedChange(change, current, entry.Value, "Restore"));
        }
        return plan;
    }

    public void Apply(IReadOnlyList<PlannedChange> plan)
    {
        if (plan.Count == 0) return;
        foreach (var p in plan) _store.Remember(p.Change, p.OldValue);
        _store.Save();

        var readOnlyTargets = plan
            .Where(p => p.Change.Kind == ChangeKind.FileReadOnly)
            .ToDictionary(p => p.Change.Target, p => p.NewValue, StringComparer.OrdinalIgnoreCase);

        foreach (var group in plan.Where(p => p.Change.Kind == ChangeKind.Ini).GroupBy(p => p.Change.Target, StringComparer.OrdinalIgnoreCase))
        {
            var file = group.Key;
            var wasReadOnly = File.Exists(file) && new FileInfo(file).IsReadOnly;
            if (wasReadOnly) File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            var doc = IniDocument.Load(file);
            foreach (var p in group)
            {
                if (p.NewValue is null) doc.Remove(p.Change.Section, p.Change.Key);
                else doc.Set(p.Change.Section, p.Change.Key, p.NewValue);
            }
            doc.Save(file);
            if (wasReadOnly && !readOnlyTargets.ContainsKey(file)) SetReadOnly(file, true);
        }

        foreach (var p in plan.Where(p => p.Change.Kind == ChangeKind.Registry))
        {
            WriteRegistry(p.Change.Target, p.Change.Key, p.NewValue);
        }

        foreach (var (file, value) in readOnlyTargets)
        {
            if (File.Exists(file)) SetReadOnly(file, value == "true");
        }
    }

    public void ForgetOriginals()
    {
        _store.Clear();
        _store.Save();
    }

    public static bool ValuesEqual(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void SetReadOnly(string file, bool on)
    {
        var attrs = File.GetAttributes(file);
        File.SetAttributes(file, on ? attrs | FileAttributes.ReadOnly : attrs & ~FileAttributes.ReadOnly);
    }

    private static (RegistryKey Root, string SubKey) SplitRegistryPath(string path)
    {
        var idx = path.IndexOf('\\');
        var hive = idx < 0 ? path : path[..idx];
        var sub = idx < 0 ? string.Empty : path[(idx + 1)..];
        var root = hive.ToUpperInvariant() switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            _ => throw new ArgumentException($"Unsupported registry hive: {hive}")
        };
        return (root, sub);
    }

    private static string? ReadRegistry(string path, string name)
    {
        var (root, sub) = SplitRegistryPath(path);
        using var key = root.OpenSubKey(sub);
        var value = key?.GetValue(name);
        if (value is null) return null;
        return key!.GetValueKind(name) == RegistryValueKind.DWord ? $"dword:{(int)value}" : $"sz:{value}";
    }

    private static void WriteRegistry(string path, string name, string? value)
    {
        var (root, sub) = SplitRegistryPath(path);
        if (value is null)
        {
            using var existing = root.OpenSubKey(sub, true);
            existing?.DeleteValue(name, false);
            return;
        }
        using var key = root.CreateSubKey(sub, true);
        if (value.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
            key.SetValue(name, int.Parse(value[6..]), RegistryValueKind.DWord);
        else
            key.SetValue(name, value.StartsWith("sz:", StringComparison.OrdinalIgnoreCase) ? value[3..] : value, RegistryValueKind.String);
    }
}
