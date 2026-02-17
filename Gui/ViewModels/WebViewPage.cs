using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

public partial class WebViewPage : ContentPage, IQueryAttributable
{
    private const string urlParam = "url", paramsParam = "params";

    internal static Task OpenUrlAsync(string url, Dictionary<string, string?>? parameters = null)
    {
        if (!url.IsValidHttpUrl())
            throw new ArgumentException(url + " is not a valid HTTP/S URL and won't be opened for your safety.", nameof(url));

        // Launcher.OpenAsync expects a properly encoded URL
        if (Shell.Current == null) return Launcher.OpenAsync(BuildUrl(url, parameters));
        else
        {
            /* Shell.GoToAsync expects raw, unencoded query parameters and encodes them.
             * Passing a URL with already encoded parameters would decode them.
             * So encoding needs to happen after receiving the raw parameters in ApplyQueryAttributes. */
            Dictionary<string, object> args = new() { [urlParam] = url };
            if (parameters != null) args.Add(paramsParam, parameters);
            return Shell.Current.GoToAsync(nameof(WebViewPage), args);
        }
    }

    private static string BuildUrl(string baseUrl, IDictionary<string, string?>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return baseUrl;

        var query = parameters.Where(p => p.Value != null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}").Join("&");

        return $"{baseUrl}?{query}";
    }

    public string? Url { get; set; }

    public WebViewPage()
    {
        BindingContext = this;

        Content = new WebView
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        }.Bind(WebView.SourceProperty, nameof(Url));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var url = query[urlParam].ToString()!; // required

        if (query.TryGetValue(paramsParam, out var parameters)) // optional
            url = BuildUrl(url, (Dictionary<string, string?>)parameters); // encodes parameters

        Url = url; // set complete URL
        OnPropertyChanged(nameof(Url)); // navigate there
    }
}
