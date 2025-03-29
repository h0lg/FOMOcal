using AngleSharp;
using AngleSharp.Dom;
using FomoCal.Gui.ViewModels;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

internal partial class EventPage : IDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    private readonly Venue venue;
    private readonly IBrowsingContext? browsingContext; // not ours to dispose, just holding on to it
    private readonly AutomatedEventPageView? loader;
    private readonly Func<string, Task<DomDoc>>? createDocumentFromHtmlAsync;
    private readonly Action? cleanup;

    internal Task<DomDoc> Loading { get; private set; }

    internal EventPage(Venue venue, IBrowsingContext browsingContext)
    {
        this.venue = venue;
        this.browsingContext = browsingContext;
        Loading = browsingContext.OpenAsync(venue.ProgramUrl);
    }

    internal EventPage(Venue venue, Layout layout, Func<string, Task<DomDoc>> createDocumentFromHtmlAsync)
    {
        this.venue = venue;
        this.createDocumentFromHtmlAsync = createDocumentFromHtmlAsync;
        loader = new(venue);
        loader.IsVisible = false;
        layout.Add(loader); // to start its lifecycle
        Loading = LoadDocument();
        cleanup = () => layout.Remove(loader); // make sure to remove loader again
    }

    internal bool HasMore() => venue.Event.LoadsMoreOnNextPage() && GetNextPageElement() != null;

    internal async Task<DomDoc?> LoadMoreAsync()
    {
        var nextPage = GetNextPageElement()!;

        if (venue.Event.PagingStrategy == Venue.PagingStrategy.NavigateLinkToLoadMore)
        {
            var href = nextPage.GetAttribute("href");
            if (href.IsNullOrWhiteSpace() || href == "#") return null;
            var url = nextPage.HyperReference(href!);

            if (browsingContext != null)
            {
                Loading = browsingContext.OpenAsync(url);
                return await Loading;
            }
            else
            {
                loader!.Source = url.ToString();
                return await LoadDocument();
            }
        }

        throw new InvalidOperationException($"{nameof(Venue.PagingStrategy)} {venue.Event.PagingStrategy} is not supported on element {nextPage}");
    }

    private Task<DomDoc> LoadDocument()
    {
        TaskCompletionSource<DomDoc> eventHtmlLoading = new();

        loader!.HtmlWithEventsLoaded += async html =>
        {
            if (html.IsSignificant())
            {
                var doc = await createDocumentFromHtmlAsync!(html!);
                eventHtmlLoading.TrySetResult(doc);
            }
            else eventHtmlLoading.SetException(new Exception(loader.EventLoadingTimedOut));
        };

        Loading = eventHtmlLoading.Task;
        return Loading;
    }

    private AngleSharp.Dom.IElement? GetNextPageElement() => Loading.Result.QuerySelector(venue.Event.NextPageSelector!);

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed) return;
        Loading.Dispose();
        cleanup?.Invoke();
        isDisposed = true;
        GC.SuppressFinalize(this); // to avoid unnecessary GC overhead
    }
}
