namespace FomoCal.Gui
{
    public partial class App : Application
    {
        private readonly IServiceProvider services;

        internal static Page CurrentPage => Current!.Windows[0].Page!;

        public App(IServiceProvider services)
        {
            this.services = services;
            InitializeComponent();

            MauiExceptions.UnhandledException += async (sender, args) =>
            {
                Exception exception = (Exception)args.ExceptionObject;
                await ErrorReport.WriteAsyncAndShare(exception.ToString(), "caught globally");
            };
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

            return new(rootPage) { Title = "😱📅 FOMOcal" };
        }

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
