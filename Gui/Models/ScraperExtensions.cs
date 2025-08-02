using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.XPath;
using FomoCal.Gui.ViewModels;
using DomDoc = AngleSharp.Dom.IDocument;
using DomElmt = AngleSharp.Dom.IElement;

namespace FomoCal;

internal static class ScraperExtensions
{
    internal static async Task<DomDoc> CreateDocumentAsync(this IBrowsingContext context, string html, Venue venue)
        => await context.OpenAsync(response =>
        {
            response.Content(html).Address(venue.ProgramUrl);
            string? encodingOverride = venue.TryGetAutomationHtmlEncoding(out var encoding) ? encoding : null;
            if (encoding.IsSignificant()) response.OverrideEncoding(encoding);
        });

    internal static IEnumerable<DomElmt> SelectEvents(this DomDoc document, Venue venue)
        => ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector)
            // see https://github.com/AngleSharp/AngleSharp.XPath
            ? document.Body.SelectNodes(xPathSelector).OfType<DomElmt>()
            : document.QuerySelectorAll(venue.Event.Selector);

    /// <summary>Adds a HTTP header to the <paramref name="response"/> that overrides
    /// e.g. a meta tag in the document source that claims an incorrect encoding
    /// with the specified <paramref name="encoding"/>,
    /// avoiding incorrect interpretation of characters when extracting text.
    /// See https://github.com/AngleSharp/AngleSharp/blob/main/docs/tutorials/06-Questions.md#how-can-i-specify-encoding-for-loading-a-document</summary>
    /// <returns>The modified <paramref name="response"/>.</returns>
    internal static AngleSharp.Io.VirtualResponse OverrideEncoding(this AngleSharp.Io.VirtualResponse response, string? encoding)
        => response.Header("content-type", "text/html; charset=" + encoding);

    internal static bool CanLoadMore(this DomDoc document, Venue venue) => venue.Event.LoadsMoreOnScrollDown()
        || (venue.Event.LoadsMoreOrDifferentOnNextPage() && document.GetNextPageElement(venue) != null);

    private static DomElmt? GetNextPageElement(this DomDoc document, Venue venue)
        => document.QuerySelector(venue.Event.NextPageSelector!);

    internal static async ValueTask<Task<DomDoc?>?> LoadMoreAsync(this IBrowsingContext browsingContext,
        Venue venue, AutomatedEventPageView? loader, DomDoc currentPage)
    {
        switch (venue.Event.PagingStrategy)
        {
            case Venue.PagingStrategy.ClickElementToLoadMore:
                ArgumentNullException.ThrowIfNull(loader);
                await loader.ClickElementToLoadMore(venue.Event.NextPageSelector!);
                return loader.LoadAutomated(browsingContext, venue);
            case Venue.PagingStrategy.NavigateLinkToLoadMore:
                var nextPage = currentPage.GetNextPageElement(venue)!;
                var href = nextPage.GetAttribute("href");
                if (href.IsNullOrWhiteSpace() || href == "#") return null; // to prevent loop
                var url = nextPage.HyperReference(href!);
                if (loader == null) return browsingContext.OpenAsync(url)!;
                loader!.Source = url.ToString();
                return loader.LoadAutomated(browsingContext, venue);
            case Venue.PagingStrategy.ScrollDownToLoadMore:
                ArgumentNullException.ThrowIfNull(loader);
                await loader.ScrollDownToLoadMore();
                return loader.LoadAutomated(browsingContext, venue);
            case Venue.PagingStrategy.ClickElementToLoadDifferent:
                ArgumentNullException.ThrowIfNull(loader);
                await loader.ClickElementToLoadDifferent(venue.Event.NextPageSelector!);
                return loader.LoadAutomated(browsingContext, venue);
            default:
                throw new InvalidOperationException($"{nameof(Venue.PagingStrategy)} {venue.Event.PagingStrategy} is not supported");
        }
    }

    /// <summary>Loads a <see cref="Venue.ProgramUrl"/> that <see cref="Venue.EventScrapeJob.RequiresAutomation"/>
    /// using an <see cref="AutomatedEventPageView"/>.</summary>
    /// <param name="throwOnTimeout">Whether to throw an exception on loading timeout.</param>
    /// <returns>The loading task.</returns>
    internal static Task<DomDoc?> LoadAutomated(this AutomatedEventPageView loader,
        IBrowsingContext browsingContext, Venue venue, bool throwOnTimeout = false)
    {
        TaskCompletionSource<DomDoc?> eventHtmlLoading = new();
        loader!.HtmlWithEventsLoaded += HandleLoaded;
        loader!.ErrorLoading += HandleError;
        return eventHtmlLoading.Task;

        async void HandleLoaded(string? html)
        {
            DetachHandlers();

            if (html.IsSignificant())
            {
                var doc = await browsingContext.CreateDocumentAsync(html!, venue);
                eventHtmlLoading.TrySetResult(doc);
            }
            else if (throwOnTimeout) eventHtmlLoading.TrySetException(new Exception(loader.EventLoadingTimedOut));
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
            loader!.HtmlWithEventsLoaded -= HandleLoaded;
            loader!.ErrorLoading -= HandleError;
        }
    }
}
