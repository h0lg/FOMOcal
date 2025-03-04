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
    }
}
