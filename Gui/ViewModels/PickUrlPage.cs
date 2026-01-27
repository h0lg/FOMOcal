namespace FomoCal.Gui.ViewModels;

public partial class PickUrlPage : ContentPage
{
    private readonly TaskCompletionSource<string?> tcs;
    private readonly WebView webView;

    internal Task<string?> Result => tcs.Task;

    internal PickUrlPage(string url)
    {
        tcs = new TaskCompletionSource<string?>();
        webView = new WebView { Source = url };
        webView.Navigated += (object? sender, WebNavigatedEventArgs e) => url = e.Url;

        ToolbarItems.Add(new ToolbarItem(Glyphs.Target + "Use this URL", null, () =>
        {
            tcs.TrySetResult(url);
            Navigation.PopAsync();
        }));

        if (Shell.Current != null)
        {
            Shell.SetTabBarIsVisible(this, false); // to avoid navigating to another tab
            Shell.SetNavBarIsVisible(this, true); // to show ToolbarItems
        }

        Content = webView;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Ensure a result is returned even if the user navigates back manually
        if (!tcs.Task.IsCompleted) tcs.TrySetResult(null);
    }
}
