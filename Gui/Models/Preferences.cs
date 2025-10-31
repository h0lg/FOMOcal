namespace FomoCal;

internal class RememberedUshort(string preferencesKey, ushort defaultValue)
{
    internal ushort Get() => (ushort)Preferences.Get(preferencesKey, defaultValue);
    internal void Set(ushort value) => Preferences.Set(preferencesKey, value);
}

internal class RememberedString(string preferencesKey, string? defaultValue = null)
{
    internal string? Get() => Preferences.Get(preferencesKey, defaultValue);
    internal void Set(string value) => Preferences.Set(preferencesKey, value);
}

internal class RememberedStrings(string preferencesKey, string separator = ",")
{
    private readonly RememberedString remembered = new(preferencesKey);
    internal string[] Get() => remembered.Get()?.Split(separator) ?? [];
    internal void Set(IEnumerable<string> values) => remembered.Set(values.Join(separator));
}

internal static class Theme
{
    private static readonly RememberedString remembered = new(nameof(AppTheme), nameof(AppTheme.Unspecified));

    internal static void Restore()
        // load persisted theme, falling back to OS theme
        => Application.Current!.UserAppTheme = Enum.TryParse<AppTheme>(remembered.Get(), out var parsed) ? parsed : AppTheme.Unspecified;

    internal static AppTheme Get() => Application.Current!.UserAppTheme;

    internal static void Set(AppTheme theme)
    {
        Application.Current!.UserAppTheme = theme;
        remembered.Set(theme.ToString());
    }
}
