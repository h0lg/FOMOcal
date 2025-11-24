using System.Text.RegularExpressions;
using System.Web;

namespace FomoCal.Gui.ViewModels;

/// <summary>A custom WebView you can load a <see cref="Venue.ProgramUrl"/> into
/// to either <see cref="EnablePicking(bool)"/> elements from
/// or automate (if it <see cref="Venue.EventScrapeJob.RequiresAutomation"/>)
/// in order to load <see cref="Event"/>s from it
/// e.g. by waiting for lazy-loaded ones via <see cref="HtmlWithEventsLoaded"/>
/// or loading more depending on the <see cref="Venue.EventScrapeJob.PagingStrategy"/>.</summary>
public partial class AutomatedEventPageView : WebView, IAutomateAnEventListing
{
    const string interopMessagePrefix = "fomocal://",
        eventsLoaded = interopMessagePrefix + "events.loaded",
        elementPicked = interopMessagePrefix + "element.picked",
        messageLogged = interopMessagePrefix + "console",
        elementPickedSelector = "selector",
        scriptApi = "FOMOcal.", picking = scriptApi + "picking.", waitForSelector = scriptApi + "waitForSelector.";

    private readonly Venue venue;
    private readonly Action<string, string?>? Log;

    /// <inheritdoc />
    public event Action<string?>? HtmlWithEventsLoaded;

    /// <inheritdoc />
    public event Action<WebNavigationResult>? ErrorLoading;

    /// <summary>An event that notifies the subscriber about a DOM node
    /// having been picked and returning its selector.</summary>
    internal event Action<string>? PickedSelector;

    private string? url;
    public string? Url { get => url; set => Source = url = value; }

    public AutomatedEventPageView(Venue venue, Action<string, string?>? log = null)
    {
        this.venue = venue;
        Log = log;

        Navigating += OnNavigatingAsync;
        Navigated += OnNavigatedAsync;
    }

    internal Task PickParent() => EvaluateJavaScriptAsync($"{picking}parent();");

    internal Task EnablePicking(bool enablePicking)
        => EvaluateJavaScriptAsync($"{picking}enable({enablePicking.ToString().ToLower()});");

    /// <summary>Tells the JS picker the context in which to pick the clicked element
    /// when building the selector to return via <see cref="PickedSelector"/>.
    /// If <paramref name="descendant"/> is true, it returns the selector
    /// of the picked element descendant relative to its closest ancestor (or self) matching <paramref name="selector"/>.
    /// Otherwise, it returns the selector for the closest common ancestor of the picked element and the <paramref name="selector"/>
    /// (relative to the document root).</summary>
    internal Task PickRelativeTo(string selector, bool descendant)
        => EvaluateJavaScriptAsync($"{picking}relativeTo('{selector}', {descendant.ToString().ToLower()});");

    internal Task SetPickedSelectorDetail(PickedSelectorOptions selectorDetail)
        => EvaluateJavaScriptAsync($"{picking}withOptions({ToJsonOptions(selectorDetail)});");

    public Task ClickElementToLoadMore(string selector)
        => EvaluateJavaScriptAsync($"{waitForSelector}afterClickingOn('{selector}', {GetWaitForSelectorOptions()});");

    public Task ClickElementToLoadDifferent(string selector)
        => EvaluateJavaScriptAsync($"{waitForSelector}mutationAfterClickingOn('{selector}', {GetWaitForSelectorOptions()});");

    public Task ScrollDownToLoadMore()
        => EvaluateJavaScriptAsync($"{waitForSelector}afterScrollingDown({GetWaitForSelectorOptions()});");

    private async void OnNavigatingAsync(object? sender, WebNavigatingEventArgs args)
    {
        /*  Using Navigating event triggered by setting location to an identifiable fixed URL in JS to call back to the host app.
            Inspired by https://stackoverflow.com/questions/73217992/js-net-interact-on-maui-webview/75182298
            See also https://github.com/dotnet/maui/issues/6446
            https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/webview?view=net-maui-9.0#invoke-javascript
            https://developer.mozilla.org/en-US/docs/Web/API/Window/location#example_1_navigate_to_a_new_page */
        if (!args.Url.StartsWith(interopMessagePrefix)) return;

        args.Cancel = true; // canceling navigation if it is used as a work-around to inter-op with the host app

        if (args.Url.StartsWith(messageLogged))
        {
            var query = HttpUtility.ParseQueryString(args.Url.Split('?')[1]);

            foreach (var level in query.AllKeys)
            {
                var message = query[level];
                if (message != null) Log?.Invoke(message, level?.ToUpper());
            }
        }
        else if (args.Url.StartsWith(eventsLoaded))
        {
            var loaded = args.Url[(args.Url.IndexOf('?') + 1)..];

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
        else if (args.Url.StartsWith(elementPicked))
        {
            var query = HttpUtility.ParseQueryString(args.Url.Split('?')[1]);
            PickedSelector?.Invoke(query[elementPickedSelector]!);
        }
    }

    private async void OnNavigatedAsync(object? sender, WebNavigatedEventArgs args)
    {
        if (args.Result != WebNavigationResult.Success)
        {
            ErrorLoading?.Invoke(args.Result); // notify awaiter
            return;
        }

        string script = await consoleHooksScript.Value;
        script += $"{scriptApi}console.hook((level, msg) => {{ {NavigateTo($"`{messageLogged}?${{level}}=${{encodeURIComponent(msg)}}`")} }});";

        if (venue.Event.RequiresAutomation())
        {
            script += await waitForSelectorScript.Value; // load script with API
            script += $"{waitForSelector}init(loaded => {{ {NavigateTo($"'{eventsLoaded}?' + loaded")} }});";
        }

        if (venue.Event.LazyLoaded)
            script += $"{waitForSelector}onLoad({GetWaitForSelectorOptions()});";
        else script += NavigateTo($"'{eventsLoaded}?true'"); // if view is used to load URL without waiting, call back immediately

        if (PickedSelector != null)
        {
            script += await pickingScript.Value; // load script with API
            const string selectorArg = "selector";
            string navigation = NavigateTo($"`{elementPicked}?{elementPickedSelector}=${{{selectorArg}}}`");
            script += $"{picking}init({selectorArg} => {{ {navigation} }});";
        }

        await EvaluateJavaScriptAsync(script);
    }

    private static string NavigateTo(string quotedUrl) => $"location = {quotedUrl};";

    internal static string GetLeafSelector(string selector, bool isXpath)
    {
        string pathDelimiter = isXpath ? "\n/" : "\n> ";
        int lastIndex = selector.LastIndexOf(pathDelimiter);
        if (lastIndex < 0) return selector;
        string displayed = selector[(lastIndex + pathDelimiter.Length)..];
        return isXpath ? "//" + displayed : displayed;
    }
}
