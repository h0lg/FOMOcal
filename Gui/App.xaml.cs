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
            Window window = new(DeviceInfo.Idiom == DeviceIdiom.Desktop ? services.GetRequiredService<MainPage>() : new AppShell());
            window.Title = "😱📅 FOMOcal";
            return window;
        }
    }
}
