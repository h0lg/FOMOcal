using AngleSharp;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

public sealed class Scraper : IDisposable
{
    private readonly IBrowsingContext context;

    public Scraper()
    {
        var config = Configuration.Default.WithDefaultLoader();
        context = BrowsingContext.New(config);
    }

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

    internal async Task<DomDoc> GetDocumentAsync(Venue venue)
        => await context.OpenAsync(venue.ProgramUrl);

    public void Dispose() => context.Dispose();
}

internal static class ScraperExtensions
{
    internal static IEnumerable<AngleSharp.Dom.IElement> SelectEvents(this DomDoc document, Venue venue)
        => document.QuerySelectorAll(venue.Event.Selector);
}
