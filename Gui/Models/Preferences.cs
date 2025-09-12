namespace FomoCal;

internal static class Theme
{
    private const string preferenceKey = nameof(AppTheme);

    internal static void Restore()
    {
        // load persisted theme, falling back to OS theme
        var saved = Preferences.Get(preferenceKey, nameof(AppTheme.Unspecified));
        Application.Current!.UserAppTheme = Enum.TryParse<AppTheme>(saved, out var parsed) ? parsed : AppTheme.Unspecified;
    }

    internal static AppTheme Get() => Application.Current!.UserAppTheme;

    internal static void Set(AppTheme theme)
    {
        Application.Current!.UserAppTheme = theme;
        Preferences.Set(preferenceKey, theme.ToString());
    }
}
