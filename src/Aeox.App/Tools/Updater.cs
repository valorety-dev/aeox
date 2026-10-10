using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Aeox.Core.Tweaks;

namespace Aeox.App.Tools;

public sealed class Updater
{
    public const string AfterUpdateArg = "--after-update";
    private const string Api = "https://api.github.com/repos/valorety-dev/aeox/releases/latest";
    private const string Asset = "Aeox.exe";
    private bool _busy;

    public static Version Current { get; } = Normalize(typeof(Updater).Assembly.GetName().Version);

    private static string Dir => Path.Combine(AeoxContext.DefaultDataDir(), "update");

    public Version? ReadyVersion { get; private set; }

    public string? ReadyPath { get; private set; }

    public bool IsReady => ReadyPath is not null;

    public event Action? Changed;

    public async Task CheckAsync()
    {
        if (_busy || IsReady) return;
        _busy = true;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"aeox/{Current}");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var doc = JsonDocument.Parse(await http.GetStringAsync(Api));
            var root = doc.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed)) return;
            var version = Normalize(parsed);
            if (version <= Current) return;

            JsonElement? asset = null;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                if (string.Equals(a.GetProperty("name").GetString(), Asset, StringComparison.OrdinalIgnoreCase)) asset = a;
            }
            if (asset is not { } found) return;
            var size = found.GetProperty("size").GetInt64();
            var url = found.GetProperty("browser_download_url").GetString();
            var digest = found.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            if (url is null || !url.StartsWith("https://github.com/valorety-dev/aeox/releases/download/", StringComparison.OrdinalIgnoreCase)) return;

            Directory.CreateDirectory(Dir);
            var target = Path.Combine(Dir, $"Aeox-{version}.exe");
            if (!Valid(target, size, digest))
            {
                var part = target + ".part";
                await using (var source = await http.GetStreamAsync(url))
                await using (var file = File.Create(part))
                {
                    await source.CopyToAsync(file);
                }
                if (!Valid(part, size, digest))
                {
                    File.Delete(part);
                    return;
                }
                File.Move(part, target, true);
            }
            ReadyVersion = version;
            ReadyPath = target;
            Changed?.Invoke();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException
                                       or TaskCanceledException or KeyNotFoundException or InvalidOperationException)
        {
        }
        finally
        {
            _busy = false;
        }
    }

    public bool TryApply(bool tray)
    {
        if (ReadyPath is null || Environment.ProcessPath is not { } exe) return false;
        var old = exe + ".old";
        try
        {
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try
            {
                File.Copy(ReadyPath, exe);
            }
            catch
            {
                File.Move(old, exe);
                throw;
            }
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, Arguments = AfterUpdateArg + (tray ? " --tray" : string.Empty) });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static void Cleanup()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && File.Exists(exe + ".old")) File.Delete(exe + ".old");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        try
        {
            if (!Directory.Exists(Dir)) return;
            foreach (var file in Directory.EnumerateFiles(Dir))
            {
                var name = Path.GetFileNameWithoutExtension(file).Replace("Aeox-", string.Empty);
                if (file.EndsWith(".part", StringComparison.OrdinalIgnoreCase) || !Version.TryParse(name, out var v) || Normalize(v) <= Current) File.Delete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static string? PendingOnDisk()
    {
        try
        {
            if (!Directory.Exists(Dir)) return null;
            return Directory.EnumerateFiles(Dir, "Aeox-*.exe")
                .Select(f => (file: f, ok: Version.TryParse(Path.GetFileNameWithoutExtension(f)["Aeox-".Length..], out var v), v))
                .Where(x => x.ok && Normalize(x.v!) > Current && FileVersionInfo.GetVersionInfo(x.file).ProductName == "Aeox")
                .OrderByDescending(x => Normalize(x.v!))
                .Select(x => x.file)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void UsePending(string path)
    {
        if (Version.TryParse(Path.GetFileNameWithoutExtension(path)["Aeox-".Length..], out var v)) ReadyVersion = Normalize(v);
        ReadyPath = path;
    }

    private static bool Valid(string file, long size, string? digest)
    {
        if (!File.Exists(file) || new FileInfo(file).Length != size) return false;
        if (FileVersionInfo.GetVersionInfo(file).ProductName != "Aeox") return false;
        if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return true;
        using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        return string.Equals(hash, digest["sha256:".Length..], StringComparison.OrdinalIgnoreCase);
    }

    private static Version Normalize(Version? v) => v is null ? new Version(0, 0, 0) : new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
