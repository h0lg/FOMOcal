namespace FomoCal;

public sealed partial class Scraper(IBrowser browser, IBuildEventListingAutomators automatorFactory) : IDisposable
{
    /// <summary>Scrapes <see cref="Event"/>s from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/>.</summary>
    public async Task<(HashSet<Event> events, List<Exception> errors)> ScrapeVenueAsync(Venue venue)
    {
        HashSet<Event> events = [];
        List<Exception> errors = [];

        VenueScrapeContext venueScrape = new(venue, browser, automatorFactory); // async errors are thrown when awaiting Loading below

        try
        {
            var document = await venueScrape.Loading;
            var pagingStrat = venue.Event.PagingStrategy;
            var nextPageSelector = pagingStrat.RequiresNextPageSelector() ? " " + venue.Event.NextPageSelector : null;
            venueScrape.Log($"loaded {document!.Url}, paging strategy loads {pagingStrat.GetDescription()}{nextPageSelector}");
            int allRelevantEvents = ScrapeEvents(venueScrape, events, errors, document!);

            /*  load more even if there are 0 relevantEvents on the first page -
             *  in case it shows the current month with no gig until the end of the month */
            while (document!.CanLoadMore(venue)) // does not reliably break loop for all loading strategies
            {
                venueScrape.Log("can load more");
                document = await venueScrape.LoadMoreAsync();
                if (document == null) break; // stop loading more if next selector doesn't go to a page or loading more times out
                venueScrape.Log($"loaded {document.Url}");
                int containedRelevantEvents = ScrapeEvents(venueScrape, events, errors, document);

                // determine number of new, scrapable events that are not already past
                int newRelevantEvents = pagingStrat.LoadsDifferentEvents()
                    ? containedRelevantEvents // all contained relevant events are considered new
                    : containedRelevantEvents - allRelevantEvents; // substract previously loaded relevant events

                venueScrape.Log($"found {containedRelevantEvents} relevant events, {newRelevantEvents} of them new");

                // stop loading more if scraping current page yielded no new relevant events to prevent loop
                if (newRelevantEvents == 0) break;

                /*  count up total relevant events to hit break condition above
                 *  for paging strats that load more instead of different events */
                allRelevantEvents += newRelevantEvents;
            }

            venueScrape.Log($"found {allRelevantEvents} relevant events in total");
        }
        catch (Exception ex)
        {
            var config = venue.Serialize();
            var log = venueScrape.GetScrapeLog();
            string message = $"Failed to scrape venue {venue.Name}\n\nConfig\n\n{config}\n\nLog\n\n{log}";
            errors.Add(new ScrapeJob.Error(message, ex));
        }
        finally
        {
            if (venueScrape.Venue.SaveScrapeLogs)
                await venueScrape.SaveScrapeLogAsync();

            venueScrape.Dispose();
        }

        return (events, errors);
    }

    private static int ScrapeEvents(VenueScrapeContext venueScrape, HashSet<Event> events, List<Exception> errors, IDomDocument document)
    {
        var venue = venueScrape.Venue;
        var selected = document.SelectEvents(venue).ToArray();
        var filtered = selected.FilterEvents(venue).ToArray();
        int irrelevant = 0; // counts unscrapable and past events in selected

        foreach (var container in selected)
        {
            var name = venue.Event.Name.GetValue(container, errors);
            DateTime? date = venue.Event.Date.GetDate(container, errors);

            if (name == null || date == null || date < DateTime.Today)
            {
                irrelevant++; // count before filtering so that filter doesn't distort the count
                continue;
            }

            if (!filtered.Contains(container)) continue; // excluded by filter

            // scrape and set properties required for equality comparison
            Event scraped = new()
            {
                Venue = venue.Name,
                Name = name,
                Date = date.Value,
                Scraped = DateTime.Now,
                Url = venue.Event.Url?.GetUrl(container, errors)
            };

            if (events.Contains(scraped)) continue; // duplicate

            // scrape details and add event
            scraped.SubTitle = venue.Event.SubTitle?.GetValue(container, errors);
            scraped.Description = venue.Event.Description?.GetValue(container, errors);
            scraped.Genres = venue.Event.Genres?.GetValue(container, errors);
            scraped.Stage = venue.Event.Stage?.GetValue(container, errors);
            scraped.DoorsTime = venue.Event.DoorsTime?.GetValue(container, errors);
            scraped.StartTime = venue.Event.StartTime?.GetValue(container, errors);
            scraped.PresalePrice = venue.Event.PresalePrice?.GetValue(container, errors);
            scraped.DoorsPrice = venue.Event.DoorsPrice?.GetValue(container, errors);
            scraped.ImageUrl = venue.Event.ImageUrl?.GetUrl(container, errors);
            scraped.TicketUrl = venue.Event.TicketUrl?.GetUrl(container, errors);
            if (scraped.Url == null) scraped.ScrapedFrom = document.Url; // for reference
            events.Add(scraped);
        }

        var msg = $"found {selected.Length} events";
        if (selected.Length != filtered.Length) msg += $", {filtered.Length} matched by {venue.Event.Filter}";
        if (irrelevant > 0) msg += $", {irrelevant} of them in the past or unscrapable";
        venueScrape.Log(msg);

        // return number of scrapable events that are not already past
        return selected.Length - irrelevant;
    }

    internal Task<IDomDocument> CreateDocumentAsync(string html, Venue venue, string? url)
        => browser.CreateDocumentAsync(html, venue, url);

    internal async Task<IDomDocument?> LoadMoreAsync(IAutomateAnEventListing automator, Venue venue, IDomDocument currentPage)
    {
        var loading = await browser.LoadMoreAsync(venue, automator, currentPage);
        if (loading == null) return null;
        return await loading;
    }

    public void Dispose() => browser.Dispose();
}
