namespace FomoCal.Gui.ViewModels;

public abstract class PickerPage<T> : ContentPage
{
    private readonly TaskCompletionSource<T?> tcs = new();

    protected void SetResult(T result)
    {
        tcs.TrySetResult(result);
        Navigation.PopAsync();
    }

    internal async Task<T?> GetResult(INavigation navigation)
    {
        await navigation.PushAsync(this);
        return await tcs.Task;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Ensure a result is returned even if the user navigates back manually
        if (!tcs.Task.IsCompleted) tcs.TrySetResult(default);
    }
}

public partial class PickUrlPage : PickerPage<string>
{
    internal PickUrlPage(string url)
    {
        WebView webView = new() { Source = url };
        webView.Navigated += (sender, e) => url = e.Url;
        ToolbarItems.Add(new ToolbarItem(Glyphs.Target + "Use this URL", null, () => SetResult(url)));

        if (Shell.Current != null)
        {
            Shell.SetTabBarIsVisible(this, false); // to avoid navigating to another tab
            Shell.SetNavBarIsVisible(this, true); // to show ToolbarItems
        }

        Content = webView;
    }
}
