using AngleSharp;
using FomoCal.Gui;
using FomoCal.Gui.ViewModels;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

public sealed partial class Scraper : IDisposable
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
        var config = Configuration.Default.WithDefaultLoader();
        context = BrowsingContext.New(config);
    }

    /// <summary>Scrapes <see cref="Event"/>s from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/>.</summary>
    public async Task<(HashSet<Event> events, Exception[] errors)> ScrapeVenueAsync(Venue venue)
    {
        HashSet<Event> events = [];
        List<Exception> errors = [];

        var eventPage = GetEventPage(venue); // async errors are thrown when awaiting Loading below

        try
        {
            var document = await eventPage.Loading;
            ScrapeEvents(venue, events, errors, document!);

            while (eventPage.CanLoadMore())
            {
                document = await eventPage.LoadMoreAsync();
                if (document == null) break; // stop loading more if next selector doesn't go to a page or loading more times out
                var added = ScrapeEvents(venue, events, errors, document);
                if (!added) break; // stop loading more if scraping current page yielded no new events to prevent loop
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ScrapeJob.Error($"Failed to scrape venue {venue.Name}", ex));
        }
        finally
        {
            eventPage.Dispose();
        }

        return (events, errors.ToArray());
    }

    private static bool ScrapeEvents(Venue venue, HashSet<Event> events, List<Exception> errors, DomDoc document)
    {
        var addedAny = false;

        foreach (var container in document.SelectEvents(venue))
        {
            var name = venue.Event.Name.GetValue(container, errors);
            DateTime? date = venue.Event.Date.GetDate(container, errors);
            if (name == null || date == null) continue;

            var scraped = new Event
            {
                Venue = venue.Name,
                Name = name,
                Date = date.Value,
                Scraped = DateTime.Now,
                SubTitle = venue.Event.SubTitle?.GetValue(container, errors),
                Description = venue.Event.Description?.GetValue(container, errors),
                Genres = venue.Event.Genres?.GetValue(container, errors),
                Stage = venue.Event.Stage?.GetValue(container, errors),
                DoorsTime = venue.Event.DoorsTime?.GetValue(container, errors),
                StartTime = venue.Event.StartTime?.GetValue(container, errors),
                PresalePrice = venue.Event.PresalePrice?.GetValue(container, errors),
                DoorsPrice = venue.Event.DoorsPrice?.GetValue(container, errors),
                Url = venue.Event.Url?.GetUrl(container, errors),
                ImageUrl = venue.Event.ImageUrl?.GetUrl(container, errors),
                TicketUrl = venue.Event.TicketUrl?.GetUrl(container, errors)
            };

            if (scraped.Url == null) scraped.ScrapedFrom = document.Url; // for reference
            addedAny = events.Add(scraped) || addedAny;
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

    internal async Task<DomDoc> CreateDocumentAsync(string html, Venue venue)
        => await context.OpenAsync(response =>
        {
            response.Content(html).Address(venue.ProgramUrl);
            string? encodingOverride = venue.TryGetAutomationHtmlEncoding(out var encoding) ? encoding : null;
            if (encoding.IsSignificant()) response.OverrideEncoding(encoding);
        });

    public void Dispose() => context.Dispose();
}
