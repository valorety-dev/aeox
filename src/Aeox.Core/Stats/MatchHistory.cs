using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aeox.Core.Game;

namespace Aeox.Core.Stats;

public sealed record MatchStat(
    DateTime TimeUtc,
    double AvgFps,
    double HitchesPerMin,
    double AvgHitchMs,
    double? GameThreadMs,
    double? RenderThreadMs,
    double? GpuMs,
    double? PingMs,
    double? JitterMs)
{
    public string Bottleneck
    {
        get
        {
            if (GameThreadMs is null || RenderThreadMs is null || GpuMs is null) return "unknown";
            var cpu = Math.Max(GameThreadMs.Value, RenderThreadMs.Value);
            if (GpuMs.Value > cpu * 1.15) return "GPU";
            if (cpu > GpuMs.Value * 1.15) return "CPU";
            return "balanced";
        }
    }
}

public sealed record ApplyMark(DateTime TimeUtc, string Summary);

public sealed class HistoryData
{
    public List<MatchStat> Matches { get; set; } = new();
    public List<ApplyMark> Applies { get; set; } = new();
}

public static partial class MatchLogParser
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static List<MatchStat> Parse(IEnumerable<string> lines)
    {
        var result = new List<MatchStat>();
        var pings = new List<double>();
        var jitters = new List<double>();
        MatchStat? current = null;

        foreach (var line in lines)
        {
            var ping = PingPattern().Match(line);
            if (ping.Success)
            {
                var p = double.Parse(ping.Groups[1].Value, Inv);
                if (p > 0) pings.Add(p);
                var jit = JitterPattern().Match(line);
                if (jit.Success) jitters.Add(double.Parse(jit.Groups[1].Value, Inv));
                continue;
            }

            var mvp = SnapshotPattern().Match(line);
            if (mvp.Success)
            {
                if (current is not null) result.Add(current);
                current = null;
                var fps = double.Parse(mvp.Groups[2].Value, Inv);
                if (fps <= 0) continue;
                current = new MatchStat(
                    ParseTime(mvp.Groups[1].Value),
                    fps,
                    double.Parse(mvp.Groups[3].Value, Inv),
                    double.Parse(mvp.Groups[4].Value, Inv),
                    null, null, null,
                    pings.Count > 0 ? pings.Average() : null,
                    jitters.Count > 0 ? jitters.Average() : null);
                pings.Clear();
                jitters.Clear();
                continue;
            }

            if (current is null) continue;
            var thread = ThreadPattern().Match(line);
            if (thread.Success)
            {
                var ms = double.Parse(thread.Groups[2].Value, Inv);
                current = thread.Groups[1].Value switch
                {
                    "GT" => current with { GameThreadMs = ms },
                    "RT" => current with { RenderThreadMs = ms },
                    "GPU" => current with { GpuMs = ms },
                    _ => current
                };
                continue;
            }
            if (line.Contains("LogHealthSnapshot: ====", StringComparison.Ordinal))
            {
                result.Add(current);
                current = null;
            }
        }
        if (current is not null) result.Add(current);
        return result;
    }

    private static DateTime ParseTime(string stamp) =>
        DateTime.SpecifyKind(DateTime.ParseExact(stamp, "yyyy.MM.dd-HH.mm.ss", Inv), DateTimeKind.Utc);

    [GeneratedRegex(@"^\[(\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}):\d{3}\]\[\s*\d+\]LogHealthSnapshot: MVP: [\d.]+%, AvgFPS:([\d.]+), HitchesPerMinute: ([\d.]+), Avg Hitch ([\d.]+)ms")]
    private static partial Regex SnapshotPattern();

    [GeneratedRegex(@"LogHealthSnapshot: (GT|RT|GPU):\s+Avg ([\d.]+)ms")]
    private static partial Regex ThreadPattern();

    [GeneratedRegex(@"PingRTT: ([\d.]+)")]
    private static partial Regex PingPattern();

    [GeneratedRegex(@"AverageJitter: ([\d.]+)")]
    private static partial Regex JitterPattern();
}

public sealed class MatchHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _path;

    private MatchHistory(string path, HistoryData data)
    {
        _path = path;
        Data = data;
    }

    public HistoryData Data { get; }

    public static MatchHistory Load(string dataDir, GameKind game)
    {
        var path = Path.Combine(dataDir, $"history-{game.ToString().ToLowerInvariant()}.json");
        HistoryData data;
        try
        {
            data = File.Exists(path) ? JsonSerializer.Deserialize<HistoryData>(File.ReadAllText(path)) ?? new HistoryData() : new HistoryData();
        }
        catch (JsonException)
        {
            data = new HistoryData();
        }
        return new MatchHistory(path, data);
    }

    public int ImportLogs(string logDir)
    {
        if (!Directory.Exists(logDir)) return 0;
        var known = Data.Matches.Select(m => m.TimeUtc).ToHashSet();
        var added = 0;
        foreach (var file in Directory.EnumerateFiles(logDir, "FortniteGame*.log"))
        {
            List<MatchStat> parsed;
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                parsed = MatchLogParser.Parse(ReadLines(reader));
            }
            catch (IOException)
            {
                continue;
            }
            foreach (var m in parsed)
            {
                if (!known.Add(m.TimeUtc)) continue;
                Data.Matches.Add(m);
                added++;
            }
        }
        if (added > 0)
        {
            Data.Matches.Sort((a, b) => a.TimeUtc.CompareTo(b.TimeUtc));
            Save();
        }
        return added;
    }

    public void AddApply(string summary)
    {
        Data.Applies.Add(new ApplyMark(DateTime.UtcNow, summary));
        Save();
    }

    public (double Before, double After, int BeforeCount, int AfterCount)? CompareAroundLastApply()
    {
        var last = Data.Applies.LastOrDefault();
        if (last is null) return null;
        var before = Data.Matches.Where(m => m.TimeUtc < last.TimeUtc).TakeLast(10).ToList();
        var after = Data.Matches.Where(m => m.TimeUtc > last.TimeUtc).ToList();
        if (before.Count == 0 || after.Count == 0) return null;
        return (before.Average(m => m.AvgFps), after.Average(m => m.AvgFps), before.Count, after.Count);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(Data, JsonOptions));
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line) yield return line;
    }
}
