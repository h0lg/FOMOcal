using AngleSharp;
using FomoCal.Gui;
using FomoCal.Gui.ViewModels;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

public sealed class Scraper : IDisposable
{
    private readonly IBrowsingContext context;
    private Layout? topLayout;

    private Layout TopLayout
    {
        get
        {
            if (topLayout == null)
            {
                topLayout = App.GetCurrentContentPage().FindTopLayout() as Layout;

                if (topLayout == null) throw new InvalidOperationException(
                    $"You need to use the {nameof(Scraper)} on a {nameof(ContentPage)} with a {nameof(Layout)} to attach the {nameof(AutomatedEventPageView)} to.");
            }

            return topLayout!;
        }
    }

    public Scraper()
    {
        var config = Configuration.Default.WithDefaultLoader().WithXPath();
        context = BrowsingContext.New(config);
    }

    /// <summary>Scrapes <see cref="Event"/>s from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/>.</summary>
    public async Task<HashSet<Event>> ScrapeVenueAsync(Venue venue)
    {
        HashSet<Event> events = [];

        var eventPage = GetEventPage(venue);

        try
        {
            var document = await eventPage.Loading;
            ScrapeEvents(venue, events, document!);

            while (eventPage.HasMore())
            {
                document = await eventPage.LoadMoreAsync();
                if (document == null) break; // stop loading more if next selector doesn't go to a page or loading more times out
                var added = ScrapeEvents(venue, events, document);
                if (!added) break; // stop loading more if scraping current page yielded no new events to prevent loop
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to scrape venue {venue.Name}: {ex.Message}");
        }
        finally
        {
            eventPage.Dispose();
        }

        return events;
    }

    private static bool ScrapeEvents(Venue venue, HashSet<Event> events, DomDoc document)
    {
        var addedAny = false;

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

            addedAny = events.Add(scrapedEvent) || addedAny;
        }

        return addedAny;
    }

    /// <summary>Loads the <see cref="DomDoc"/> from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/> for scraping.
    /// If scraping is configured to <see cref="Venue.EventScrapeJob.WaitForJsRendering"/>,
    /// a new <see cref="AutomatedEventPageView"/> is added to <see cref="App.GetCurrentContentPage"/>.
    /// That view loads the URL and waits until the <see cref="Venue.EventScrapeJob.Selector"/> matches anything,
    /// which is when it is removed again.</summary>
    private EventPage GetEventPage(Venue venue)
        => venue.Event.RequiresAutomation()
            ? new EventPage(venue, TopLayout, CreateDocumentAsync)
            : new EventPage(venue, context);

    internal async Task<DomDoc> CreateDocumentAsync(string html) => await context.OpenAsync(req => req.Content(html));

    public void Dispose() => context.Dispose();
}

internal static class ScraperExtensions
{
    internal static IEnumerable<AngleSharp.Dom.IElement> SelectEvents(this DomDoc document, Venue venue)
        => document.QuerySelectorAll(venue.Event.Selector);
}
