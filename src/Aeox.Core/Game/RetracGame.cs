using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Aeox.Core.Game;

public sealed class GamePaths
{
    public GamePaths(string savedDir)
    {
        SavedDir = savedDir;
        ConfigDir = Path.Combine(savedDir, "Config", "WindowsClient");
        GameUserSettings = Path.Combine(ConfigDir, "GameUserSettings.ini");
        Engine = Path.Combine(ConfigDir, "Engine.ini");
        Input = Path.Combine(ConfigDir, "Input.ini");
        LogDir = Path.Combine(savedDir, "Logs");
        GameLog = Path.Combine(LogDir, "FortniteGame.log");
    }

    public string SavedDir { get; }
    public string ConfigDir { get; }
    public string GameUserSettings { get; }
    public string Engine { get; }
    public string Input { get; }
    public string LogDir { get; }
    public string GameLog { get; }

    public bool ConfigExists => File.Exists(GameUserSettings);
}

public enum GameKind
{
    Retrac,
    Fortnite
}

public sealed class GameProfile
{
    public const string RetracId = "Retrac";
    public const string LiveId = "Fortnite";

    private readonly Func<string?> _findExe;

    public GameProfile(string id, string shortName, string? version, bool isLive, GamePaths paths, Func<string?> findExe)
    {
        Id = id;
        ShortName = shortName;
        Version = version;
        IsLive = isLive;
        Paths = paths;
        _findExe = findExe;
    }

    public string Id { get; }
    public string ShortName { get; }
    public string? Version { get; }
    public bool IsLive { get; }
    public GamePaths Paths { get; }
    public bool IsFortnite { get; init; } = true;
    public IReadOnlyList<string> SettingsFiles { get; init; } = Array.Empty<string>();
    public Func<Aeox.Core.Tweaks.AeoxContext, IReadOnlyList<Aeox.Core.Tweaks.Tweak>>? Tweaks { get; init; }
    public string? Note { get; init; }

    public bool ConfigExists => IsFortnite ? Paths.ConfigExists : SettingsFiles.Any(File.Exists);

    public string ConfigLocation => IsFortnite ? Paths.ConfigDir
        : SettingsFiles.Count == 0 ? "No settings file. Aeox only sets Windows options for this game." : string.Join(Environment.NewLine, SettingsFiles);

    public double? VersionNumber =>
        Version is not null && double.TryParse(Version, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    public string? Season => IsLive ? null : GameCatalog.SeasonFor(VersionNumber);

    public string Name => !IsFortnite ? ShortName : IsLive
        ? "Fortnite  ·  Epic Games"
        : $"{ShortName}  ·  Fortnite {Version ?? "unknown version"}" + (Season is null ? string.Empty : $"  ·  {Season}");

    public string Label => !IsFortnite ? ShortName.ToLowerInvariant() : IsLive ? "fortnite" : Version is null ? ShortName.ToLowerInvariant() : $"{ShortName.ToLowerInvariant()} {Version}";

    public bool SupportsEngineTweaks => IsFortnite && !IsLive && (VersionNumber is null || VersionNumber < 19);

    public bool HasPerformanceMode => IsFortnite && IsLive || VersionNumber >= 19;

    public bool SupportsReflex => !IsFortnite || IsLive || VersionNumber is null || VersionNumber >= 14.1;

    public string HistoryKey => GameCatalog.HistoryKey(Paths.SavedDir);

    public string? FindExe() => _findExe();

    public bool IsInstalled => ConfigExists || FindExe() is not null;

    public static GameProfile Retrac(string? savedDir = null) => new(
        RetracId, "Retrac", "14.60", false,
        new GamePaths(savedDir ?? Path.Combine(LocalAppData, "RetracGame", "Saved")),
        FindRetracExe);

    public static GameProfile Fortnite(string? savedDir = null) => new(
        LiveId, "Fortnite", null, true,
        new GamePaths(savedDir ?? Path.Combine(LocalAppData, "FortniteGame", "Saved")),
        FindEpicFortniteExe);

    public static GameProfile For(GameKind kind) => kind == GameKind.Fortnite ? Fortnite() : Retrac();

    internal static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    internal static string? FindRetracExe()
    {
        var root = Path.Combine(LocalAppData, "launcher.retrac.site");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root)
            .Select(d => Path.Combine(d, "FortniteGame", "Binaries", "Win64", GameRunning.GameProcess + ".exe"))
            .FirstOrDefault(File.Exists);
    }

    internal static string? FindEpicFortniteExe()
    {
        var manifest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
        if (!File.Exists(manifest)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            foreach (var app in doc.RootElement.GetProperty("InstallationList").EnumerateArray())
            {
                if (app.GetProperty("AppName").GetString() != "Fortnite") continue;
                var exe = Path.Combine(app.GetProperty("InstallLocation").GetString() ?? string.Empty,
                    "FortniteGame", "Binaries", "Win64", GameRunning.GameProcess + ".exe");
                if (File.Exists(exe)) return exe;
            }
        }
        catch (JsonException)
        {
        }
        catch (KeyNotFoundException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        return null;
    }
}

public static class GameRunning
{
    public const string GameProcess = "FortniteClient-Win64-Shipping";

    public static bool IsGameRunning() => Process.GetProcessesByName(GameProcess).Length > 0;
}

public static class RetracGame
{
    public const string GameProcess = GameRunning.GameProcess;
    public const string UserSettingsSection = "/Script/FortniteGame.FortGameUserSettings";
    public const string ConsoleVariablesSection = "ConsoleVariables";
    public const string ScalabilitySection = "ScalabilityGroups";
    public const string InputSection = "/Script/Engine.InputSettings";
    public const string RhiSection = "D3DRHIPreference";

    public static bool IsGameRunning() => GameRunning.IsGameRunning();
}
