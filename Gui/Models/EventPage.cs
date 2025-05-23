using AngleSharp;
using AngleSharp.Dom;
using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using Microsoft.Maui.Layouts;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

internal partial class EventPage : IDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    private readonly Venue venue;
    private readonly IBrowsingContext? browsingContext; // not ours to dispose, just holding on to it
    private readonly AutomatedEventPageView? loader;
    private readonly Func<string, Task<DomDoc>>? createDocumentFromHtmlAsync;
    private readonly Action? cleanup;

    internal Task<DomDoc?> Loading { get; private set; }

    internal EventPage(Venue venue, IBrowsingContext browsingContext)
    {
        this.venue = venue;
        this.browsingContext = browsingContext;
        Loading = browsingContext.OpenAsync(venue.ProgramUrl)!;
    }

    internal EventPage(Venue venue, Layout layout, Func<string, Task<DomDoc>> createDocumentFromHtmlAsync)
    {
        this.venue = venue;
        this.createDocumentFromHtmlAsync = createDocumentFromHtmlAsync;

        const int height = 1000, width = 1000;

        /* Add loader to an AbsoluteLayout that lets it have a decent size and be IsVisible
         * (which some pages require to properly scroll and load more events)
         * while staying out of view and not taking up space in the layout it's added to. */
        loader = new AutomatedEventPageView(venue)
            //.LayoutBounds(0, 0, width, height) // use to see what's going on
            .LayoutBounds(width, height, width, height) // position off-screen with a decent size
            .LayoutFlags(AbsoluteLayoutFlags.None);

        AbsoluteLayout wrapper = new() { WidthRequest = 0, HeightRequest = 0 };
        wrapper.Add(loader);
        layout.Add(wrapper); // to start the loader's lifecycle

        Loading = LoadDocument(throwOnTimeout: true); // first page of events is required
        cleanup = () => layout.Remove(wrapper); // make sure to remove loader again
    }

    internal bool HasMore() => venue.Event.LoadsMoreOnScrollDown()
        || (venue.Event.LoadsMoreOnNextPage() && GetNextPageElement() != null);

    internal async Task<DomDoc?> LoadMoreAsync()
    {
        switch (venue.Event.PagingStrategy)
        {
            case Venue.PagingStrategy.ClickElementToLoadMore:
                await loader!.ClickElementToLoadMore(venue.Event.NextPageSelector!);
                Loading = LoadDocument();
                break;
            case Venue.PagingStrategy.NavigateLinkToLoadMore:
                var nextPage = GetNextPageElement()!;
                var href = nextPage.GetAttribute("href");
                if (href.IsNullOrWhiteSpace() || href == "#") return null; // to prevent loop
                var url = nextPage.HyperReference(href!);

                if (browsingContext != null)
                    Loading = browsingContext.OpenAsync(url)!;
                else
                {
                    loader!.Source = url.ToString();
                    Loading = LoadDocument();
                }

                break;
            case Venue.PagingStrategy.ScrollDownToLoadMore:
                await loader!.ScrollDownToLoadMore();
                Loading = LoadDocument();
                break;
            case Venue.PagingStrategy.ClickElementToLoadDifferent:
                await loader!.ClickElementToLoadDifferent(venue.Event.NextPageSelector!);
                Loading = LoadDocument();
                break;
            default:
                throw new InvalidOperationException($"{nameof(Venue.PagingStrategy)} {venue.Event.PagingStrategy} is not supported");
        }

        return await Loading;
    }

    private Task<DomDoc?> LoadDocument(bool throwOnTimeout = false)
    {
        TaskCompletionSource<DomDoc?> eventHtmlLoading = new();

        loader!.HtmlWithEventsLoaded += async html =>
        {
            if (html.IsSignificant())
            {
                var doc = await createDocumentFromHtmlAsync!(html!);
                eventHtmlLoading.TrySetResult(doc);
            }
            else if (throwOnTimeout) eventHtmlLoading.TrySetException(new Exception(loader.EventLoadingTimedOut));
            else eventHtmlLoading.TrySetResult(null);
        };

        return eventHtmlLoading.Task;
    }

    private AngleSharp.Dom.IElement? GetNextPageElement() => Loading.Result?.QuerySelector(venue.Event.NextPageSelector!);

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed) return;
        Loading.Dispose();
        cleanup?.Invoke();
        isDisposed = true; // allow GC to clean up the rest
    }
}
