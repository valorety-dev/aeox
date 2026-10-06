using System.Diagnostics;
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
    private readonly Func<string?> _findExe;

    private GameProfile(GameKind kind, string name, string shortName, GamePaths paths, bool supportsEngineTweaks, bool hasPerformanceMode, Func<string?> findExe)
    {
        Kind = kind;
        Name = name;
        ShortName = shortName;
        Paths = paths;
        SupportsEngineTweaks = supportsEngineTweaks;
        HasPerformanceMode = hasPerformanceMode;
        _findExe = findExe;
    }

    public GameKind Kind { get; }
    public string Name { get; }
    public string ShortName { get; }
    public GamePaths Paths { get; }
    public bool SupportsEngineTweaks { get; }
    public bool HasPerformanceMode { get; }

    public string? FindExe() => _findExe();

    public bool IsInstalled => Paths.ConfigExists || FindExe() is not null;

    public static GameProfile Retrac(string? savedDir = null) => new(
        GameKind.Retrac, "Retrac  ·  Fortnite 14.60", "Retrac",
        new GamePaths(savedDir ?? Path.Combine(LocalAppData, "RetracGame", "Saved")),
        supportsEngineTweaks: true, hasPerformanceMode: false,
        FindRetracExe);

    public static GameProfile Fortnite(string? savedDir = null) => new(
        GameKind.Fortnite, "Fortnite  ·  Epic Games", "Fortnite",
        new GamePaths(savedDir ?? Path.Combine(LocalAppData, "FortniteGame", "Saved")),
        supportsEngineTweaks: false, hasPerformanceMode: true,
        FindEpicFortniteExe);

    public static GameProfile For(GameKind kind) => kind == GameKind.Fortnite ? Fortnite() : Retrac();

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string? FindRetracExe()
    {
        var root = Path.Combine(LocalAppData, "launcher.retrac.site");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root)
            .Select(d => Path.Combine(d, "FortniteGame", "Binaries", "Win64", GameRunning.GameProcess + ".exe"))
            .FirstOrDefault(File.Exists);
    }

    private static string? FindEpicFortniteExe()
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
