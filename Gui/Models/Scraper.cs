﻿using AngleSharp;
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
            int allRelevantEvents = ScrapeEvents(venue, events, errors, document!);

            /*  load more even if there are 0 relevantEvents on the first page -
             *  in case it shows the current month with no gig until the end of the month */
            while (document!.CanLoadMore(venue)) // does not reliably break loop for all loading strategies
            {
                document = await eventPage.LoadMoreAsync();
                if (document == null) break; // stop loading more if next selector doesn't go to a page or loading more times out
                int containedRelevantEvents = ScrapeEvents(venue, events, errors, document);

                // determine number of new, scrapable events that are not already past
                int newRelevantEvents = venue.Event.PagingStrategy.LoadsDifferentEvents()
                    ? containedRelevantEvents // all contained relevant events are considered new
                    : containedRelevantEvents - allRelevantEvents; // substract previously loaded relevant events

                // stop loading more if scraping current page yielded no new relevant events to prevent loop
                if (newRelevantEvents == 0) break;

                /*  count up total relevant events to hit break condition above
                 *  for paging strats that load more instead of different events */
                allRelevantEvents += newRelevantEvents;
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

    private static int ScrapeEvents(Venue venue, HashSet<Event> events, List<Exception> errors, DomDoc document)
    {
        var unfiltered = document.SelectEvents(venue).ToArray();
        var filtered = unfiltered.FilterEvents(venue).ToArray();
        int irrelevant = 0; // counts unscrapable and past events in unfiltered

        foreach (var container in unfiltered)
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

        // return number of scrapable events that are not already past
        return unfiltered.Length - irrelevant;
    }

    /// <summary>Loads the <see cref="DomDoc"/> from the <paramref name="venue"/>'s <see cref="Venue.ProgramUrl"/> for scraping.
    /// If events are <see cref="Venue.EventScrapeJob.LazyLoaded"/>,
    /// a new <see cref="AutomatedEventPageView"/> is added to <see cref="App.GetCurrentContentPage"/>.
    /// That view loads the URL and waits until the <see cref="Venue.EventScrapeJob.Selector"/> matches anything,
    /// which is when it is removed again.</summary>
    private EventPage GetEventPage(Venue venue)
        => venue.Event.RequiresAutomation()
            ? new EventPage(venue, context, TopLayout)
            : new EventPage(venue, context);

    internal Task<DomDoc> CreateDocumentAsync(string html, Venue venue) => context.CreateDocumentAsync(html, venue);

    internal async Task<DomDoc?> LoadMoreAsync(AutomatedEventPageView loader, Venue venue, DomDoc currentPage)
    {
        var loading = await context.LoadMoreAsync(venue, loader, currentPage);
        if (loading == null) return null;
        return await loading;
    }

    public void Dispose() => context.Dispose();
}
