using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

[QueryProperty(nameof(Url), nameof(Url))]
public partial class WebViewPage : ContentPage
{
    internal static Task OpenUrlAsync(string url)
    {
        if (!url.IsValidHttpUrl())
            throw new ArgumentException(url + " is not a valid HTTP/S URL and won't be opened for your safety.", nameof(url));

        return Shell.Current == null ? Launcher.OpenAsync(url)
            : Shell.Current.GoToAsync($"{nameof(WebViewPage)}?{nameof(Url)}={Uri.EscapeDataString(url)}");
    }

    string url = "";
    public string Url
    {
        get => url;
        set
        {
            url = value;
            OnPropertyChanged();
        }
    }

    public WebViewPage()
    {
        BindingContext = this;

        Content = new WebView
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        }.Bind(WebView.SourceProperty, nameof(Url));
    }
}
