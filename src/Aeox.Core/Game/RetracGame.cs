using System.Diagnostics;

namespace Aeox.Core.Game;

public sealed class RetracPaths
{
    public RetracPaths(string savedDir)
    {
        SavedDir = savedDir;
        ConfigDir = Path.Combine(savedDir, "Config", "WindowsClient");
        GameUserSettings = Path.Combine(ConfigDir, "GameUserSettings.ini");
        Engine = Path.Combine(ConfigDir, "Engine.ini");
        Input = Path.Combine(ConfigDir, "Input.ini");
        GameLog = Path.Combine(savedDir, "Logs", "FortniteGame.log");
    }

    public string SavedDir { get; }
    public string ConfigDir { get; }
    public string GameUserSettings { get; }
    public string Engine { get; }
    public string Input { get; }
    public string GameLog { get; }

    public bool ConfigExists => File.Exists(GameUserSettings);

    public static RetracPaths Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetracGame", "Saved"));
}

public static class RetracGame
{
    public const string GameProcess = "FortniteClient-Win64-Shipping";
    public const string LauncherProcess = "retrac";
    public const string UserSettingsSection = "/Script/FortniteGame.FortGameUserSettings";
    public const string ConsoleVariablesSection = "ConsoleVariables";
    public const string ScalabilitySection = "ScalabilityGroups";
    public const string InputSection = "/Script/Engine.InputSettings";

    public static bool IsGameRunning() => Process.GetProcessesByName(GameProcess).Length > 0;

    public static string? FindGameExe()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "launcher.retrac.site");
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root)
            .Select(d => Path.Combine(d, "FortniteGame", "Binaries", "Win64", GameProcess + ".exe"))
            .FirstOrDefault(File.Exists);
    }
}
