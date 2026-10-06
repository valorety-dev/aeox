using System.Globalization;
using System.Management;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aeox.Core.Drivers;

public sealed record InstalledGpu(string Name, string RawVersion, DateTime? DriverDate)
{
    public bool IsNvidia => Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase);

    public string FriendlyVersion => IsNvidia ? NvidiaVersion(RawVersion) : RawVersion;

    public static string NvidiaVersion(string raw)
    {
        var digits = raw.Replace(".", string.Empty);
        if (digits.Length < 5) return raw;
        var tail = digits[^5..];
        return $"{tail[..3]}.{tail[3..]}";
    }
}

public sealed record DriverRelease(string Version, DateTime Released, string Name, string Headline, string DownloadUrl, string? DetailsUrl);

public static partial class GpuDrivers
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static IReadOnlyList<InstalledGpu> Installed()
    {
        var list = new List<InstalledGpu>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate FROM Win32_VideoController");
            foreach (var o in searcher.Get())
            {
                var name = (o["Name"] as string ?? string.Empty).Trim();
                var version = o["DriverVersion"] as string ?? string.Empty;
                DateTime? date = null;
                if (o["DriverDate"] is string d && d.Length >= 8 &&
                    DateTime.TryParseExact(d[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    date = parsed;
                if (name.Length > 0) list.Add(new InstalledGpu(name, version, date));
            }
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
        }
        return list;
    }

    public static async Task<IReadOnlyList<DriverRelease>> NvidiaReleasesAsync(string gpuName, int count = 20)
    {
        var ids = await FindNvidiaProductAsync(gpuName);
        if (ids is null) return Array.Empty<DriverRelease>();
        var url = "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php" +
                  $"?func=DriverManualLookup&psid={ids.Value.Series}&pfid={ids.Value.Product}&osID=57&languageCode=1033" +
                  $"&beta=0&isWHQL=1&dltype=-1&dch=1&upCRD=0&qnf=0&sort1=0&numberOfResults={count}";
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
        if (!doc.RootElement.TryGetProperty("IDS", out var idsArray)) return Array.Empty<DriverRelease>();
        var releases = new List<DriverRelease>();
        foreach (var item in idsArray.EnumerateArray())
        {
            if (!item.TryGetProperty("downloadInfo", out var info)) continue;
            var version = info.GetProperty("Version").GetString() ?? string.Empty;
            var released = DateTime.TryParseExact(info.GetProperty("ReleaseDateTime").GetString(), "ddd MMM dd, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var r) ? r : DateTime.MinValue;
            var name = Uri.UnescapeDataString(info.GetProperty("Name").GetString() ?? "GeForce driver");
            var download = info.GetProperty("DownloadURL").GetString() ?? string.Empty;
            var headline = info.TryGetProperty("ReleaseNotes", out var n) ? Headline(Uri.UnescapeDataString(n.GetString() ?? string.Empty)) : string.Empty;
            var details = info.TryGetProperty("DetailsURL", out var du) ? du.GetString() : null;
            releases.Add(new DriverRelease(version, released, name, headline, download, details));
        }
        return releases;
    }

    private static async Task<(int Series, int Product)?> FindNvidiaProductAsync(string gpuName)
    {
        var xml = XDocument.Parse(await Http.GetStringAsync("https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3"));
        var wanted = Normalize(gpuName);
        foreach (var v in xml.Descendants("LookupValue"))
        {
            var name = v.Element("Name")?.Value ?? string.Empty;
            if (Normalize(name) != wanted) continue;
            var parent = v.Attribute("ParentID")?.Value ?? v.Element("ParentID")?.Value;
            if (int.TryParse(v.Element("Value")?.Value, out var product) &&
                int.TryParse(parent, out var series))
                return (series, product);
        }
        return null;
    }

    private static string Normalize(string s) => WhiteSpace().Replace(s.Replace("NVIDIA", string.Empty, StringComparison.OrdinalIgnoreCase), " ").Trim().ToUpperInvariant();

    private static string Headline(string html)
    {
        var m = BoldPattern().Match(html);
        var text = m.Success ? m.Groups[1].Value : TagPattern().Replace(html, " ");
        text = WhiteSpace().Replace(System.Net.WebUtility.HtmlDecode(text), " ").Trim();
        return text.Length > 90 ? text[..90] + "..." : text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex("<b>(.*?)</b>", RegexOptions.Singleline)]
    private static partial Regex BoldPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();
}
