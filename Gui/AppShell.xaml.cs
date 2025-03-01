using FomoCal.Gui.ViewModels;

namespace FomoCal.Gui
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(WebViewPage), typeof(WebViewPage));
        }
    }
}
