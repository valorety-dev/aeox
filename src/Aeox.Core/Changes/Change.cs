namespace Aeox.Core.Changes;

public enum ChangeKind
{
    Ini,
    Registry,
    FileReadOnly
}

public sealed record Change(ChangeKind Kind, string Target, string Section, string Key, string? Value, string? Default = null)
{
    public string Id => $"{Kind}|{Target}|{Section}|{Key}".ToLowerInvariant();

    public string TargetName => Kind == ChangeKind.Registry ? Target : Path.GetFileName(Target);

    public static Change Ini(string file, string section, string key, string? value, string? gameDefault = null) =>
        new(ChangeKind.Ini, file, section, key, value, gameDefault);

    public static Change ReadOnly(string file, bool readOnly) =>
        new(ChangeKind.FileReadOnly, file, string.Empty, "read-only", readOnly ? "true" : "false", "false");

    public static Change Registry(string keyPath, string valueName, string? value, string? systemDefault = null) =>
        new(ChangeKind.Registry, keyPath, string.Empty, valueName, value, systemDefault);
}

public enum RevertKind
{
    None,
    Original,
    GameDefault
}

public sealed record PlannedChange(Change Change, string? OldValue, string? NewValue, string Source, RevertKind Revert = RevertKind.None);
