using AngleSharp;
using FomoCal.Gui;
using FomoCal.Gui.ViewModels;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

public sealed class Scraper : IDisposable
{
    private readonly IBrowsingContext context;

    public Scraper()
    {
        var config = Configuration.Default.WithDefaultLoader().WithXPath();
        context = BrowsingContext.New(config);
    }

    /// <summary>Scrapes <see cref="Event"/>s from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/>.</summary>
    public async Task<List<Event>> ScrapeVenueAsync(Venue venue)
    {
        var events = new List<Event>();

        try
        {
            var document = await GetDocumentAsync(venue);

            foreach (var eventElement in document.SelectEvents(venue))
            {
                var name = venue.Event.Name.GetValue(eventElement);
                DateTime? date = venue.Event.Date.GetDate(eventElement);
                if (name == null || date == null) continue;

                var scrapedEvent = new Event
                {
                    Venue = venue.Name,
                    Name = name,
                    Date = date.Value,
                    Scraped = DateTime.Now,
                    SubTitle = venue.Event.SubTitle?.GetValue(eventElement),
                    Description = venue.Event.Description?.GetValue(eventElement),
                    Genres = venue.Event.Genres?.GetValue(eventElement),
                    Stage = venue.Event.Stage?.GetValue(eventElement),
                    DoorsTime = venue.Event.DoorsTime?.GetValue(eventElement),
                    StartTime = venue.Event.StartTime?.GetValue(eventElement),
                    PresalePrice = venue.Event.PresalePrice?.GetValue(eventElement),
                    DoorsPrice = venue.Event.DoorsPrice?.GetValue(eventElement),
                    Url = venue.Event.Url?.GetUrl(eventElement),
                    ImageUrl = venue.Event.ImageUrl?.GetUrl(eventElement),
                    TicketUrl = venue.Event.TicketUrl?.GetUrl(eventElement)
                };

                events.Add(scrapedEvent);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to scrape venue {venue.Name}: {ex.Message}");
        }

        return events;
    }

    /// <summary>Loads the <see cref="DomDoc"/> from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/> for scraping.
    /// If scraping is configured to <see cref="Venue.EventScrapeJob.WaitForJsRendering"/>,
    /// a new <see cref="AutomatedEventPageView"/> is added to <see cref="App.GetCurrentContentPage"/>.
    /// That view loads the URL and waits until the <see cref="Venue.EventScrapeJob.Selector"/> matches anything,
    /// which is when it is removed again.</summary>
    internal async Task<DomDoc> GetDocumentAsync(Venue venue)
    {
        if (!venue.Event.WaitForJsRendering)
            return await context.OpenAsync(venue.ProgramUrl);

        AutomatedEventPageView loader = new(venue);
        loader.IsVisible = false;
        var layout = App.GetCurrentContentPage().FindTopLayout() as Layout;
        layout!.Add(loader); // to start its lifecycle

        try { return await GetDocument(loader); }
        finally { layout.Remove(loader); } // make sure to remove loader again
    }

    private Task<DomDoc> GetDocument(AutomatedEventPageView loader)
    {
        TaskCompletionSource<DomDoc> eventHtmlLoading = new();

        loader.HtmlWithEventsLoaded += async html =>
        {
            if (html.IsSignificant())
            {
                var doc = await CreateDocumentAsync(html!);
                eventHtmlLoading.TrySetResult(doc);
            }
            else eventHtmlLoading.SetException(new Exception(loader.EventLoadingTimedOut));
        };

        return eventHtmlLoading.Task;
    }

    internal async Task<DomDoc> CreateDocumentAsync(string html) => await context.OpenAsync(req => req.Content(html));

    public void Dispose() => context.Dispose();
}

internal static class ScraperExtensions
{
    internal static IEnumerable<AngleSharp.Dom.IElement> SelectEvents(this DomDoc document, Venue venue)
        => document.QuerySelectorAll(venue.Event.Selector);
}
