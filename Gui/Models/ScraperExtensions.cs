namespace FomoCal;

internal static class ScraperExtensions
{
    /// <summary>A pre-formatted error message including <see cref="venue"/> details
    /// - for when <see cref="IAutomateAnEventListing.HtmlWithEventsLoaded"/> returns null.</summary>
    internal static string FormatEventLoadingTimedOut(this Venue venue)
        => $"Waiting for event container '{venue.Event.Selector}' to be available after loading '{venue.ProgramUrl}' timed out.";

    internal static async Task<IDomDocument> CreateDocumentAsync(this IBrowser browser, string html, Venue venue, string? url = null)
        => await browser.OpenAsync(response =>
        {
            response.Content(html).Address(url ?? venue.ProgramUrl);
            string? encodingOverride = venue.TryGetAutomationHtmlEncoding(out var encoding) ? encoding : null;
            if (encoding.IsSignificant()) response.OverrideEncoding(encoding);
        });

    internal static IEnumerable<IDomElement> SelectEvents(this IDomDocument document, Venue venue)
        => ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector)
            ? document.SelectNodes(xPathSelector).OfType<IDomElement>()
            : document.QuerySelectorAll(venue.Event.Selector);

    internal static IEnumerable<IDomElement> FilterEvents(this IEnumerable<IDomElement> unfiltered, Venue venue)
    {
        if (venue.Event.Filter.IsNullOrWhiteSpace()) return unfiltered;

        var filter = venue.Event.Filter!;
        var events = unfiltered.ToArray();

        return ScrapeJob.TryGetXPathSelector(filter, out var xPathFilter)
            ? events.Where(el => el.SelectNodes(xPathFilter).Count > 0)
            // fallback: treat as text substring filter
            : events.Where(el => el.TextContent?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Adds a HTTP header to the <paramref name="builder"/> that overrides
    /// e.g. a meta tag in the document source that claims an incorrect encoding
    /// with the specified <paramref name="encoding"/>,
    /// avoiding incorrect interpretation of characters when extracting text.
    /// See https://github.com/AngleSharp/AngleSharp/blob/main/docs/tutorials/06-Questions.md#how-can-i-specify-encoding-for-loading-a-document</summary>
    /// <returns>The modified <paramref name="builder"/>.</returns>
    internal static IResponseBuilder OverrideEncoding(this IResponseBuilder builder, string? encoding)
        => builder.Header("content-type", "text/html; charset=" + encoding);

    internal static bool CanLoadMore(this IDomDocument document, Venue venue) => venue.Event.LoadsMoreOnScrollDown()
        || (venue.Event.LoadsMoreOrDifferentOnNextPage() && document.GetNextPageElement(venue) != null);

    private static IDomElement? GetNextPageElement(this IDomDocument document, Venue venue)
        => document.QuerySelector(venue.Event.NextPageSelector!);

    internal static async ValueTask<Task<IDomDocument?>?> LoadMoreAsync(this IBrowser browser,
        Venue venue, IAutomateAnEventListing? automator, IDomDocument currentPage, Action<string, string?>? log = null)
    {
        switch (venue.Event.PagingStrategy)
        {
            case Venue.PagingStrategy.ClickElementToLoadMore:
                ArgumentNullException.ThrowIfNull(automator);
                Task<IDomDocument?> loadingMore = automator.LoadAutomated(browser, venue);
                await automator.ClickElementToLoadMore(venue.Event.NextPageSelector!);
                return loadingMore;
            case Venue.PagingStrategy.NavigateLinkToLoadDifferent:
                var nextPage = currentPage.GetNextPageElement(venue)!;
                var href = nextPage.GetAttribute("href");
                log?.Invoke("next page link goes to " + href, null);
                if (href.IsNullOrWhiteSpace() || href == "#") return null; // to prevent loop
                var url = nextPage.HyperReference(href!);
                if (automator == null) return browser.OpenAsync(url!)!;
                Task<IDomDocument?> loadingPage = automator.LoadAutomated(browser, venue);
                automator!.Url = url;
                return loadingPage;
            case Venue.PagingStrategy.ScrollDownToLoadMore:
                ArgumentNullException.ThrowIfNull(automator);
                Task<IDomDocument?> loadingScrolled = automator.LoadAutomated(browser, venue);
                await automator.ScrollDownToLoadMore();
                return loadingScrolled;
            case Venue.PagingStrategy.ClickElementToLoadDifferent:
                ArgumentNullException.ThrowIfNull(automator);
                Task<IDomDocument?> loadingReplaced = automator.LoadAutomated(browser, venue);
                await automator.ClickElementToLoadDifferent(venue.Event.NextPageSelector!);
                return loadingReplaced;
            default:
                throw new InvalidOperationException($"{nameof(Venue.PagingStrategy)} {venue.Event.PagingStrategy} is not supported");
        }
    }

    /// <summary>Loads a <see cref="Venue.ProgramUrl"/> that <see cref="Venue.EventScrapeJob.RequiresAutomation"/>
    /// using an <see cref="IAutomateAnEventListing"/>.</summary>
    /// <param name="throwOnTimeout">Whether to throw an exception on loading timeout.</param>
    /// <returns>The loading task.</returns>
    internal static Task<IDomDocument?> LoadAutomated(this IAutomateAnEventListing automator,
        IBrowser browser, Venue venue, bool throwOnTimeout = false)
    {
        TaskCompletionSource<IDomDocument?> eventHtmlLoading = new();
        automator.HtmlWithEventsLoaded += HandleLoaded;
        automator.ErrorLoading += HandleError;
        return eventHtmlLoading.Task;

        async void HandleLoaded(string? html)
        {
            DetachHandlers();

            if (html.IsSignificant())
            {
                var doc = await browser.CreateDocumentAsync(html!, venue, automator.Url);
                eventHtmlLoading.TrySetResult(doc);
            }
            else if (throwOnTimeout) eventHtmlLoading.TrySetException(new Exception(venue.FormatEventLoadingTimedOut()));
            else eventHtmlLoading.TrySetResult(null);
        }

        void HandleError(WebNavigationResult navigationResult)
        {
            DetachHandlers();

            if (!throwOnTimeout && navigationResult == WebNavigationResult.Timeout)
                eventHtmlLoading.TrySetResult(null);
            else
            {
                string suffix = navigationResult == WebNavigationResult.Cancel ? "ed" : "";
                var message = $"navigation {navigationResult}{suffix}";
                eventHtmlLoading.TrySetException(new Exception(message));
            }
        }

        void DetachHandlers()
        {
            automator.HtmlWithEventsLoaded -= HandleLoaded;
            automator.ErrorLoading -= HandleError;
        }
    }
}
