namespace FomoCal.Gui
{
    public partial class App : Application
    {
        private readonly IServiceProvider services;

        internal static Page CurrentPage => Current!.Windows[0].Page!;
        internal static bool HasInternet => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        public App(IServiceProvider services)
        {
            this.services = services;
            InitializeComponent();

            MauiExceptions.UnhandledException += async (sender, args) =>
            {
                Exception exception = (Exception)args.ExceptionObject;
                await ErrorReport.WriteAsyncAndShare(exception.ToString(), "caught globally");
            };

            Theme.Restore(); // ASAP to prevent flicker
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            bool isSmall = DeviceInfo.Idiom != DeviceIdiom.Desktop;

            Page rootPage;

            if (isSmall) rootPage = new AppShell();
            else
            {
                var mainPage = services.GetRequiredService<MainPage>();
                NavigationPage.SetHasNavigationBar(mainPage, false);
                rootPage = new NavigationPage(mainPage);
            }

            Window window = new(rootPage) { Title = "FOMOcal" };

#if WINDOWS
            window.Created += async (_, _) =>
            {
                await EnsureButtonsAreVisible(window);
                Current!.RequestedThemeChanged += async (o, e) => await EnsureButtonsAreVisible(window);
            };
#endif

            return window;
        }

#if WINDOWS
        // from https://github.com/dotnet/maui/blob/main/src/Controls/src/Core/Application/Application.Windows.cs
        public static readonly Windows.UI.Color LightHoverBackground = Microsoft.UI.ColorHelper.FromArgb(24, 0, 0, 0);
        private static readonly Windows.UI.Color DarkHoverBackground = Microsoft.UI.ColorHelper.FromArgb(24, byte.MaxValue, byte.MaxValue, byte.MaxValue);

        /// <summary>Applies styles to the Window buttons after <see cref="Application.RequestedThemeChanged"/>
        /// to make them visible while <see cref="AppWindowTitleBar.BackgroundColor"/> is controlled by the system theme.</summary>
        private static async ValueTask EnsureButtonsAreVisible(Window window)
        {
            var nativeWindow = window.Handler?.PlatformView as MauiWinUIWindow;
            if (nativeWindow?.AppWindow?.TitleBar is not Microsoft.UI.Windowing.AppWindowTitleBar titleBar) return;
            await Task.Delay(50); // to run after the default OnRequestedThemeChangedPlatform handler
            bool systemDark = Current?.PlatformAppTheme == AppTheme.Dark;
            bool dark = Current?.RequestedTheme == AppTheme.Dark;

            if (systemDark && !dark)
            {
                titleBar.ButtonHoverForegroundColor =
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White; // so icons are visible
                titleBar.ButtonHoverBackgroundColor = DarkHoverBackground; // so they visually react to hover
            }

            if (!systemDark)
            {
                titleBar.ButtonHoverForegroundColor =
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black; // so icons are visible
                titleBar.ButtonHoverBackgroundColor = LightHoverBackground; // so they visually react to hover
            }
        }
#endif

        internal static Page GetCurrentContentPage()
        {
            var page = CurrentPage;

            return page switch
            {
                NavigationPage nav => nav.CurrentPage,
                Shell shell => shell.CurrentPage,
                _ => page
            };
        }
    }
}
