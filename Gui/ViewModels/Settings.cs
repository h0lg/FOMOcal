using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class Settings : ObservableObject
{
    [ObservableProperty] public partial AppTheme UserTheme { get; set; } = Theme.Get();
    partial void OnUserThemeChanged(AppTheme value) => Theme.Set(value);

    public partial class Page : ContentPage
    {
        public Page(Settings model)
        {
            BindingContext = model;
            Title = "Settings";

            Content = Grd(cols: [Auto, Star], rows: [Auto], spacing: 5,
                Lbl("Theme").CenterVertical(), ThemeSwitches().Column(1)).Center();
        }

        private static HorizontalStackLayout ThemeSwitches()
            => HStack(0, ThemeVariantToggle("🌑 dark", AppTheme.Dark, "always use dark theme"),
                ThemeVariantToggle("🌓 switch with OS", AppTheme.Unspecified,
                    "use light or dark depending on the theme variant selected on the operating system level"),
                ThemeVariantToggle("🌕 light", AppTheme.Light, "always use light theme"));

        private static RadioButton ThemeVariantToggle(string label, AppTheme theme, string tooltip)
            => new RadioButton { Content = label, StyleClass = ["SingleSelectToggleButton"] }.ToolTip(tooltip)
                .Bind(RadioButton.IsCheckedProperty, nameof(UserTheme), mode: BindingMode.TwoWay,
                    convert: (AppTheme t) => t == theme, convertBack: isChecked => isChecked ? theme : Theme.Get());
    }
}

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
