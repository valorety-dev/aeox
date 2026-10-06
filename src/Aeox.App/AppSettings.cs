using System.IO;
using System.Text.Json;
using Aeox.Core.Game;
using Aeox.Core.Tweaks;

namespace Aeox.App;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public GameKind Game { get; set; } = GameKind.Retrac;
    public bool StartWithWindows { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool AutoReapply { get; set; } = true;
    public bool SessionReports { get; set; } = true;
    public List<string> ActiveIds { get; set; } = new();

    private static string FilePath => Path.Combine(AeoxContext.DefaultDataDir(), "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (JsonException)
        {
        }
        var fresh = new AppSettings();
        if (!GameProfile.Retrac().IsInstalled && GameProfile.Fortnite().IsInstalled) fresh.Game = GameKind.Fortnite;
        return fresh;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
