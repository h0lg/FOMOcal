using System.Text.RegularExpressions;

namespace FomoCal.Gui.ViewModels;

public partial class AutomatedEventPageView : WebView
{
    const string interopMessagePrefix = "https://fomocal.",
        eventsLoaded = interopMessagePrefix + "events.loaded";

    private readonly Venue venue;

    /// <summary>An event that notifies the subscriber about the DOM from <see cref="Venue.ProgramUrl"/>
    /// being ready for scraping and returning its HTML - or null if it should
    /// <see cref="Venue.EventScrapeJob.WaitForJsRendering"/> and that times out.</summary>
    internal event Action<string?>? HtmlWithEventsLoaded;

    /// <summary>A pre-formatted error message including <see cref="venue"/> details
    /// - for when <see cref="HtmlWithEventsLoaded"/> returns null.</summary>
    internal string EventLoadingTimedOut => $"Waiting for event container '{venue.Event.Selector}' to be available after loading '{venue.ProgramUrl}' timed out.";

    public AutomatedEventPageView(Venue venue)
    {
        this.venue = venue;
        Source = venue.ProgramUrl;
        Navigating += OnNavigatingAsync;
        Navigated += OnNavigatedAsync;
    }

    private async void OnNavigatingAsync(object? sender, WebNavigatingEventArgs args)
    {
        /*  Using Navigating event triggered by setting location to an identifyable fixed URL in JS to call back to the host app.
            Inspired by https://stackoverflow.com/questions/73217992/js-net-interact-on-maui-webview/75182298
            See also https://github.com/dotnet/maui/issues/6446
            https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/webview?view=net-maui-9.0#invoke-javascript
            https://developer.mozilla.org/en-US/docs/Web/API/Window/location#example_1_navigate_to_a_new_page */
        if (!args.Url.StartsWith(interopMessagePrefix)) return;

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

                HtmlWithEventsLoaded?.Invoke(html); // notify awaiter
            }
        }
    }

    private async void OnNavigatedAsync(object? sender, WebNavigatedEventArgs args)
    {
        if (args.Result != WebNavigationResult.Success) return;
        string script = await inlinedScript.Value; // load script with API

        // append function call. check every 100ms for 100 times 10sec
        script += $"waitForSelector('{venue.Event.Selector}', {100}, loaded => {{ location = '{eventsLoaded}?' + loaded; }}, 100);";

        await EvaluateJavaScriptAsync(script);
    }

    /*  Used to cache the loaded and pre-processed script while allowing for a
     *  thread-safe asynchronous lazy initialization that only ever happens once. */
    private static readonly Lazy<Task<string>> inlinedScript = new(LoadAndInlineScriptAsync);

    private static async Task<string> LoadAndInlineScriptAsync()
    {
        // see https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers#bundled-files
        await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("waitForSelector.js");
        using StreamReader reader = new(fileStream);
        string script = await reader.ReadToEndAsync();

        return script.RemoveJsComments() // so that inline comments don't comment out code during normalization
            .NormalizeWhitespace() // to inline it; multi-line scripts seem to not be supported
            .Replace("\\", "\\\\"); // to escape the JS for EvaluateJavaScriptAsync
    }
}
