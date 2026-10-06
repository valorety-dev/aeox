using System.Text;

namespace Aeox.Core.Ini;

public sealed class IniDocument
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private readonly List<string> _lines;

    public IniDocument(IEnumerable<string> lines)
    {
        _lines = lines.ToList();
    }

    public IReadOnlyList<string> Lines => _lines;

    public static IniDocument Load(string path) =>
        new(File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>());

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllLines(path, _lines, Utf8NoBom);
    }

    public string? Get(string section, string key)
    {
        var range = FindSection(section);
        if (range is null) return null;
        var idx = FindKey(range.Value.Start, range.Value.End, key);
        if (idx < 0) return null;
        var line = _lines[idx];
        return line[(line.IndexOf('=') + 1)..].Trim();
    }

    public void Set(string section, string key, string value)
    {
        var range = FindSection(section);
        if (range is null)
        {
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0) _lines.Add(string.Empty);
            _lines.Add($"[{section}]");
            _lines.Add($"{key}={value}");
            return;
        }

        var idx = FindKey(range.Value.Start, range.Value.End, key);
        if (idx >= 0)
        {
            _lines[idx] = $"{key}={value}";
            return;
        }

        var insert = range.Value.End;
        while (insert > range.Value.Start + 1 && _lines[insert - 1].Trim().Length == 0) insert--;
        _lines.Insert(insert, $"{key}={value}");
    }

    public bool Remove(string section, string key)
    {
        var range = FindSection(section);
        if (range is null) return false;
        var idx = FindKey(range.Value.Start, range.Value.End, key);
        if (idx < 0) return false;
        _lines.RemoveAt(idx);
        return true;
    }

    private (int Start, int End)? FindSection(string section)
    {
        var header = $"[{section}]";
        for (var i = 0; i < _lines.Count; i++)
        {
            if (!string.Equals(_lines[i].Trim(), header, StringComparison.OrdinalIgnoreCase)) continue;
            var end = _lines.Count;
            for (var j = i + 1; j < _lines.Count; j++)
            {
                if (_lines[j].TrimStart().StartsWith('['))
                {
                    end = j;
                    break;
                }
            }
            return (i, end);
        }
        return null;
    }

    private int FindKey(int start, int end, string key)
    {
        for (var j = start + 1; j < end; j++)
        {
            var eq = _lines[j].IndexOf('=');
            if (eq > 0 && string.Equals(_lines[j][..eq].Trim(), key, StringComparison.OrdinalIgnoreCase)) return j;
        }
        return -1;
    }
}
