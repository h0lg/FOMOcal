using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

public partial class AutomatedEventPageView : WebView
{
    const string interopMessagePrefix = "https://fomocal.",
        eventsLoaded = interopMessagePrefix + "events.loaded",
        elementPicked = interopMessagePrefix + "element.picked",
        elementPickedSelector = "selector",
        scriptApi = "FOMOcal.", picking = scriptApi + "picking.", waitForSelector = scriptApi + "waitForSelector.";

    private readonly Venue venue;

    /* check every 200ms for 25 resetting iterations,
    * i.e. wait for approx. 5sec for JS rendering or scrolling down to load more before timing out
    * while a change in the number of matched events resets the iterations (and wait time)
    * until we time out or load at least 100 events. */
    private readonly WaitForSelectorOptions waitForSelectorOptions = new() { IntervalDelayMs = 200, MaxMatches = 100, MaxTries = 25 };

    /// <summary>An event that notifies the subscriber about the DOM from <see cref="Venue.ProgramUrl"/>
    /// being ready for scraping and returning its HTML - or null if it should
    /// <see cref="Venue.EventScrapeJob.WaitForJsRendering"/> and that times out.</summary>
    internal event Action<string?>? HtmlWithEventsLoaded;

    /// <summary>An event that notifies the subscriber about a DOM node
    /// having been picked and returning its selector.</summary>
    internal event Action<string>? PickedSelector;

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

    internal Task PickParent() => EvaluateJavaScriptAsync($"{picking}parent();");

    internal Task EnablePicking(bool enablePicking)
        => EvaluateJavaScriptAsync($"{picking}enable({enablePicking.ToString().ToLower()});");

    /// <summary>Tells the JS picker the context in which to pick the clicked element
    /// when building the selector to return via <see cref="PickedSelector"/>.
    /// If <paramref name="descendant"/> is true, it returns the selector
    /// of the picked element descendant relative to its closest ancestor (or self) matching <paramref name="selector"/>.
    /// Otherwise, it returns the selector for the closest common ancestor of the picked element and the <paramref name="selector"/>.</summary>
    internal Task PickRelativeTo(string selector, bool descendant)
        => EvaluateJavaScriptAsync($"{picking}relativeTo('{selector}', {descendant.ToString().ToLower()});");

    private static readonly JsonSerializerOptions jsonOptionSerializerOptions
        = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal void SetPickedSelectorDetail(PickedSelectorOptions selectorDetail)
    {
        var json = JsonSerializer.Serialize(selectorDetail, jsonOptionSerializerOptions);
        EvaluateJavaScriptAsync($"{picking}withOptions({json});");
    }

    internal Task ClickElementToLoadMore(string selector)
        => EvaluateJavaScriptAsync($"{waitForSelector}afterClickingOn('{selector}', {GetWaitForSelectorOptions()});");

    internal Task ClickElementToLoadDifferent(string selector)
        => EvaluateJavaScriptAsync($"{waitForSelector}mutationAfterClickingOn('{selector}', {GetWaitForSelectorOptions()});");

    internal Task ScrollDownToLoadMore()
        => EvaluateJavaScriptAsync($"{waitForSelector}afterScrollingDown({GetWaitForSelectorOptions()});");

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
        else if (args.Url.StartsWith(elementPicked))
        {
            var query = HttpUtility.ParseQueryString(args.Url.Split('?')[1]);
            PickedSelector?.Invoke(query[elementPickedSelector]!);
        }
    }

    private async void OnNavigatedAsync(object? sender, WebNavigatedEventArgs args)
    {
        if (args.Result != WebNavigationResult.Success) return;
        string script = "";

        if (venue.Event.RequiresAutomation())
        {
            script += await waitForSelectorScript.Value; // load script with API
            script += $"{waitForSelector}init(loaded => {{ {NavigateTo($"'{eventsLoaded}?' + loaded")} }});";
        }

        if (venue.Event.WaitForJsRendering)
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

    private string GetWaitForSelectorOptions()
    {
        var xpathMatch = ScrapeJob.XpathSelectorPattern().Match(venue.Event.Selector);
        waitForSelectorOptions.IsXpathSelector = xpathMatch.Success;
        waitForSelectorOptions.Selector = xpathMatch.Success ? xpathMatch.Value : venue.Event.Selector;
        return JsonSerializer.Serialize(waitForSelectorOptions, jsonOptionSerializerOptions);
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

    /*  Used to cache the loaded and pre-processed script while allowing for a
     *  thread-safe asynchronous lazy initialization that only ever happens once. */
    private static readonly Lazy<Task<string>> waitForSelectorScript = new(() => LoadAndInlineScriptAsync("waitForSelector.js"));
    private static readonly Lazy<Task<string>> pickingScript = new(() => LoadAndInlineScriptAsync("picking.js"));

    private static async Task<string> LoadAndInlineScriptAsync(string fileName)
    {
        // see https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers#bundled-files
        await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);
        using StreamReader reader = new(fileStream);
        string script = await reader.ReadToEndAsync();

        return script.RemoveJsComments() // so that inline comments don't comment out code during normalization
            .NormalizeWhitespace() // to inline it; multi-line scripts seem to not be supported
            .Replace("\\", "\\\\"); // to escape the JS for EvaluateJavaScriptAsync
    }

    internal partial class WaitForSelectorOptions : ObservableObject
    {
        [ObservableProperty] public partial string Selector { get; set; }
        [ObservableProperty] public partial bool IsXpathSelector { get; set; }
        [ObservableProperty] public partial uint IntervalDelayMs { get; set; }
        [ObservableProperty] public partial uint MaxMatches { get; set; }
        [ObservableProperty] public partial uint MaxTries { get; set; }
    }

    internal partial class PickedSelectorOptions : ObservableObject
    {
        [ObservableProperty] public partial bool XPathSyntax { get; set; }
        [ObservableProperty] public partial bool TagName { get; set; }
        [ObservableProperty] public partial bool Ids { get; set; }
        [ObservableProperty] public partial bool SemanticClasses { get; set; }
        [ObservableProperty] public partial bool LayoutClasses { get; set; }
        [ObservableProperty] public partial bool OtherAttributes { get; set; }
        [ObservableProperty] public partial bool OtherAttributeValues { get; set; }
        [ObservableProperty] public partial bool Position { get; set; }
    }
}
