using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Markup;

namespace FomoCal.Gui.ViewModels;

public partial class AutomatedEventPageView : WebView
{
    public AutomatedEventPageView(Venue venue, TaskCompletionSource<string?> eventHtmlLoading)
    {
        const string interopMessagePrefix = "https://fomocal.",
            eventsLoaded = interopMessagePrefix + "events.loaded";

        Source = venue.ProgramUrl;

        Navigating += async (sender, args) =>
        {
            /*  Using Navigating event triggered by setting location to an identifyable fixed URL in JS to call back to the host app.
                Inspired by https://stackoverflow.com/questions/73217992/js-net-interact-on-maui-webview/75182298
                See also https://github.com/dotnet/maui/issues/6446
                https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/webview?view=net-maui-9.0#invoke-javascript
                https://developer.mozilla.org/en-US/docs/Web/API/Window/location#example_1_navigate_to_a_new_page */
            if (args.Url.StartsWith(interopMessagePrefix))
            {
                args.Cancel = true; // cancelling navigation if it is used as a work-around to interop with the host app

                if (args.Url.StartsWith(eventsLoaded))
                {
                    var loaded = args.Url.Substring(args.Url.IndexOf('?') + 1);

                    if (bool.TryParse(loaded, out var isLoaded))
                    {
                        string? html = null;

                        if (isLoaded)
                        {
                            // retrieve complete document HTML once event container selector is available
                            var encodedHtml = await EvaluateJavaScriptAsync("document.documentElement.outerHTML");
                            html = Regex.Unescape(encodedHtml);
                        }

                        eventHtmlLoading.TrySetResult(html); // notify awaiter
                    }
                }
            }
        };

        Navigated += async (sender, args) =>
        {
            if (args.Result == WebNavigationResult.Success)
            {
                // load script declaring function
                await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("waitForSelector.js");
                using StreamReader reader = new(fileStream);
                var script = await reader.ReadToEndAsync();

                // append function call. check every 100ms for 100 times 10sec
                script += $"waitForSelector('{venue.Event.Selector}', {100}, loaded => {{ location = '{eventsLoaded}?' + loaded; }}, 100);";

                // normalize script to inline it; multi-line scripts seem to not be supported
                await EvaluateJavaScriptAsync(script.NormalizeWhitespace());
            }
        };
    }

    internal static CollectionView RenderAll(string pathToObservableElementItemsSource)
        => new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var contentPresenter = new ContentView();
                contentPresenter.Bind(ContentView.ContentProperty, ".");
                return contentPresenter;
            })
        }.Bind(ItemsView.ItemsSourceProperty, pathToObservableElementItemsSource);
}
