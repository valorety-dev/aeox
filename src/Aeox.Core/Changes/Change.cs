namespace Aeox.Core.Changes;

public enum ChangeKind
{
    Ini,
    Registry,
    FileReadOnly,
    PowerPlan,
    Adapter
}

public sealed record Change(ChangeKind Kind, string Target, string Section, string Key, string? Value, string? Default = null)
{
    public string Id => $"{Kind}|{Target}|{Section}|{Key}".ToLowerInvariant();

    public string TargetName => Kind is ChangeKind.Registry or ChangeKind.Adapter ? Target : Path.GetFileName(Target);

    public bool NeedsAdmin => Kind == ChangeKind.Adapter ||
                              (Kind == ChangeKind.Registry && (Target.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase) || Target.StartsWith(@"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase)));

    public static Change Ini(string file, string section, string key, string? value, string? gameDefault = null) =>
        new(ChangeKind.Ini, file, section, key, value, gameDefault);

    public static Change ReadOnly(string file, bool readOnly) =>
        new(ChangeKind.FileReadOnly, file, string.Empty, "read-only", readOnly ? "true" : "false", "false");

    public static Change Registry(string keyPath, string valueName, string? value, string? systemDefault = null) =>
        new(ChangeKind.Registry, keyPath, string.Empty, valueName, value, systemDefault);

    public static Change Adapter(string keyPath, string deviceId, string keyword, string? value, string? driverDefault = null) =>
        new(ChangeKind.Adapter, keyPath, deviceId, keyword, value, driverDefault);

    public static Change PowerPlan(string schemeGuid, string defaultGuid) =>
        new(ChangeKind.PowerPlan, "power", string.Empty, "active-scheme", schemeGuid, defaultGuid);
}

public enum RevertKind
{
    None,
    Original,
    GameDefault
}

public sealed record PlannedChange(Change Change, string? OldValue, string? NewValue, string Source, RevertKind Revert = RevertKind.None);
