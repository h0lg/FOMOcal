namespace FomoCal;

public sealed partial class Scraper(IBrowser browser, IBuildEventListingAutomators automatorFactory, ISaveScrapeLogFiles logFileSaver) : IDisposable
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
            ushort skippedTotal = ScrapeEvents(venueScrape, events, errors, document!);

            /*  load more even if there are 0 scrapable events on the first page -
             *  in case it shows the current month with no gig until the end of the month */
            while (document!.CanLoadMore(venue)) // does not reliably exit the loop for all loading strategies
            {
                venueScrape.Log("can load more");
                document = await venueScrape.LoadMoreAsync();
                if (document == null) break; // stop loading more if next selector doesn't go to a page or loading more times out
                venueScrape.Log($"loaded {document.Url}");
                int scrapedBefore = events.Count;
                ushort skipped = ScrapeEvents(venueScrape, events, errors, document);

                int skippedDiff = pagingStrat.LoadsDifferentEvents() ? skipped // all skipped events are considered different
                    : skipped - skippedTotal; // when loading more, skipped contains previously skipped events

                // sanity-check the paging strategy to make sure the loop exits
                if (skippedDiff < 0) throw new InvalidOperationException(
                    "The number of events excluded by the filter dropped during paging." +
                    $" That's not expected behavior for the configured paging strategy loading {pagingStrat.GetDescription()}." +
                    " Consider trying a different one.");

                /* stop loading more if scraping current page yielded no different scrapable events
                 * to exit the loop early and avoid paging rubbish */
                if (events.Count == scrapedBefore && skippedDiff == 0) break;

                /*  keep track of total skipped events to enable exit condition above
                 *  for paging strats that load more instead of different events */
                skippedTotal += (ushort)skippedDiff;
            }

            venueScrape.Log($"scraped {events.Count} events in total");
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
                await logFileSaver.SaveScrapeLogAsync(venueScrape.Venue, venueScrape.GetScrapeLog());

            venueScrape.Dispose();
        }

        return (events, errors);
    }

    /// <summary>Searches the <paramref name="document"/> for event containers matching the <see cref="Venue.EventScrapeJob.Selector"/>,
    /// discardes unscrapable (past, missing a name or date - or containing already scraped, duplicated event info),
    /// excludes containers not matching <see cref="Venue.EventScrapeJob.Filter"/> and returning their count,
    /// scrapes the remaining containers and adds their info to <paramref name="events"/> on success.</summary>
    /// <param name="venueScrape">Provides the scrape config and a log shared across potentially multiple calls of this method
    /// - depending on the <see cref="Venue.EventScrapeJob.PagingStrategy"/>.</param>
    /// <param name="events">Accumulates the events that were successfully scraped from the venue, potentially from multiple pages.
    /// I.e. the caller can compare the count before and after to find out if any event was scraped from the current page.</param>
    /// <param name="errors">Accumulates the errors that happened scraping the venue, potentially from multiple pages.</param>
    /// <param name="document">Represents the current page containing all, more or different events
    /// depending on the <see cref="Venue.EventScrapeJob.PagingStrategy"/>.</param>
    /// <returns>The number of event containers excluded by <see cref="Venue.EventScrapeJob.Filter"/>,
    /// which the caller can use to decide whether it makes sense to continue paging for more events.</returns>
    private static ushort ScrapeEvents(VenueScrapeContext venueScrape, HashSet<Event> events, List<Exception> errors, IDomDocument document)
    {
        var venue = venueScrape.Venue;
        var selected = document.SelectEvents(venue).ToArray();
        var filtered = selected.FilterEvents(venue).ToArray();

        // skipped event counters
        ushort past = 0, missingNameOrDate = 0, duplicate = 0, // unscrapable
            excluded = 0; // scrapable

        foreach (var container in selected)
        {
            var name = venue.Event.Name.GetValue(container, errors);
            DateTime? date = venue.Event.Date.GetDate(container, errors);

            // count and skip unscrapable before filtering - so that filter doesn't distort the count
            if (name == null || date == null)
            {
                missingNameOrDate++;
                continue;
            }

            if (date < DateTime.Today)
            {
                past++;
                continue;
            }

            // scrape and set properties required for equality comparison
            Event scraped = new()
            {
                Venue = venue.Name,
                Name = name,
                Date = date.Value,
                Scraped = DateTime.Now,
                Url = venue.Event.Url?.GetUrl(container, errors)
            };

            if (events.Contains(scraped))
            {
                duplicate++;
                continue;
            }

            if (!filtered.Contains(container))
            {
                excluded++;
                continue;
            }

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

        // log skipped events to help debug the scrape process
        var msg = $"selected {selected.Length} events";
        if (missingNameOrDate > 0) msg += $" -{missingNameOrDate} missing a name or date";
        if (past > 0) msg += $" -{past} in the past";
        if (duplicate > 0) msg += $" -{duplicate} already scraped";
        if (excluded > 0) msg += $" -{excluded} not matching '{venue.Event.Filter}'";
        venueScrape.Log(msg);

        /* The caller is only interested in scrapable, but skipped events to decide whether to continue paging.
         * Unscrapable events are dropped as if they didn't exist.
         * This allows the caller to detect when it's paging through rubbish and stop early. */
        return excluded;
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
