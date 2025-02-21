namespace FomoCal.Gui
{
    public partial class App : Application
    {
        private readonly IServiceProvider services;

        public App(IServiceProvider services)
        {
            this.services = services;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            bool isSmall = DeviceInfo.Idiom != DeviceIdiom.Desktop;

            Page rootPage = isSmall ? new AppShell()
                : new NavigationPage(services.GetRequiredService<MainPage>());

            return new(rootPage) { Title = "😱📅 FOMOcal" };
        }
    }
}
