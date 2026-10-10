using System.Text;
using System.Text.RegularExpressions;

namespace Aeox.Core.Ini;

public static class TextSettings
{
    public const string Quoted = "quoted";
    public const string Colon = "colon";
    public const string Space = "space";
    public const string Cst = "cst";
    public const string XmlValue = "xml-value";
    public const string XmlCvar = "xml-cvar";
    public const string LooseIni = "loose-ini";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static string? Get(string path, string format, string key)
    {
        if (!File.Exists(path)) return null;
        var lines = File.ReadAllLines(path);
        var idx = Find(lines, format, key, out var match);
        return idx < 0 ? null : match!.Groups["v"].Value;
    }

    public static void Apply(string path, IEnumerable<(string Format, string Key, string? Value)> edits)
    {
        if (!File.Exists(path)) return;
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var endsWithNewline = text.EndsWith('\n');
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (endsWithNewline) lines.RemoveAt(lines.Count - 1);

        foreach (var (format, key, value) in edits)
        {
            if (value is null) continue;
            var arr = lines.ToArray();
            var idx = Find(arr, format, key, out var match);
            if (idx >= 0)
            {
                var g = match!.Groups["v"];
                lines[idx] = lines[idx][..g.Index] + value + lines[idx][(g.Index + g.Length)..];
            }
            else if (Insert(lines, format, key, value) is false)
            {
                continue;
            }
        }

        var output = string.Join(newline, lines) + (endsWithNewline ? newline : string.Empty);
        File.WriteAllText(path, output, bom ? new UTF8Encoding(true) : Utf8NoBom);
    }

    private static int Find(IReadOnlyList<string> lines, string format, string key, out Match? match)
    {
        var (sectionPrefix, name) = SplitKey(format, key);
        var pattern = Pattern(format, name);
        var inSection = sectionPrefix is null;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (sectionPrefix is not null && line.TrimStart().StartsWith('['))
            {
                inSection = line.Trim().TrimStart('[').StartsWith(sectionPrefix, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection) continue;
            var m = pattern.Match(line);
            if (!m.Success) continue;
            match = m;
            return i;
        }
        match = null;
        return -1;
    }

    private static bool Insert(List<string> lines, string format, string key, string value)
    {
        switch (format)
        {
            case Colon:
                lines.Add($"{key}:{value}");
                return true;
            case Space:
                lines.Add($"{key} {value}");
                return true;
            case Quoted:
                var close = lines.FindLastIndex(l => l.Trim() == "}");
                if (close < 0) return false;
                lines.Insert(close, $"\t\"{key}\"\t\t\"{value}\"");
                return true;
            default:
                return false;
        }
    }

    private static (string? Section, string Name) SplitKey(string format, string key)
    {
        if (format != LooseIni) return (null, key);
        var slash = key.IndexOf('/');
        return slash < 0 ? (null, key) : (key[..slash], key[(slash + 1)..]);
    }

    private static Regex Pattern(string format, string key)
    {
        var k = Regex.Escape(key);
        return format switch
        {
            Quoted => new Regex($"^\\s*\"{k}\"\\s+\"(?<v>[^\"]*)\"", RegexOptions.IgnoreCase),
            Colon => new Regex($"^{k}:(?<v>.*)$"),
            Space => new Regex($"^{k}\\s+(?<v>\\S.*?)\\s*$"),
            Cst => new Regex($"^{k}:[\\d.]+\\s*=\\s*\"(?<v>[^\"]*)\""),
            XmlValue => new Regex($"<{k}\\s+value=\"(?<v>[^\"]*)\"", RegexOptions.IgnoreCase),
            XmlCvar => new Regex($"<cvar\\s+name=\"{k}\"\\s+value=\"(?<v>[^\"]*)\"", RegexOptions.IgnoreCase),
            LooseIni => new Regex($"^\\s*{k}\\s*=\\s*\"?(?<v>[^\"\\r\\n]*?)\"?\\s*$", RegexOptions.IgnoreCase),
            _ => throw new ArgumentException($"Unknown settings format {format}")
        };
    }
}
