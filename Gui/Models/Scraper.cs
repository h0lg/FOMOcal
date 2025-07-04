﻿using AngleSharp;
using AngleSharp.XPath;
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

            while (eventPage.HasMore())
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

        foreach (var eventElement in document.SelectEvents(venue))
        {
            var name = venue.Event.Name.GetValue(eventElement, errors);
            DateTime? date = venue.Event.Date.GetDate(eventElement, errors);
            if (name == null || date == null) continue;

            var scrapedEvent = new Event
            {
                Venue = venue.Name,
                Name = name,
                Date = date.Value,
                Scraped = DateTime.Now,
                SubTitle = venue.Event.SubTitle?.GetValue(eventElement, errors),
                Description = venue.Event.Description?.GetValue(eventElement, errors),
                Genres = venue.Event.Genres?.GetValue(eventElement, errors),
                Stage = venue.Event.Stage?.GetValue(eventElement, errors),
                DoorsTime = venue.Event.DoorsTime?.GetValue(eventElement, errors),
                StartTime = venue.Event.StartTime?.GetValue(eventElement, errors),
                PresalePrice = venue.Event.PresalePrice?.GetValue(eventElement, errors),
                DoorsPrice = venue.Event.DoorsPrice?.GetValue(eventElement, errors),
                Url = venue.Event.Url?.GetUrl(eventElement, errors),
                ImageUrl = venue.Event.ImageUrl?.GetUrl(eventElement, errors),
                TicketUrl = venue.Event.TicketUrl?.GetUrl(eventElement, errors)
            };

            if (scrapedEvent.Url == null) scrapedEvent.ScrapedFrom = venue.ProgramUrl; // for reference
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

    internal async Task<DomDoc> CreateDocumentAsync(string html, string? encoding = null)
        => await context.OpenAsync(response =>
        {
            response.Content(html);
            if (encoding.IsSignificant()) response.OverrideEncoding(encoding);
        });

    public void Dispose() => context.Dispose();
}

internal static class ScraperExtensions
{
    internal static IEnumerable<AngleSharp.Dom.IElement> SelectEvents(this DomDoc document, Venue venue)
        => ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector)
            // see https://github.com/AngleSharp/AngleSharp.XPath
            ? document.Body.SelectNodes(xPathSelector).OfType<AngleSharp.Dom.IElement>()
            : document.QuerySelectorAll(venue.Event.Selector);

    /// <summary>Adds a HTTP header to the <paramref name="response"/> that overrides
    /// e.g. a meta tag in the document source that claims an incorrect encoding
    /// with the specified <paramref name="encoding"/>,
    /// avoiding incorrect interpretation of characters when extracting text.
    /// See https://github.com/AngleSharp/AngleSharp/blob/main/docs/tutorials/06-Questions.md#how-can-i-specify-encoding-for-loading-a-document</summary>
    /// <returns>The modified <paramref name="response"/>.</returns>
    internal static AngleSharp.Io.VirtualResponse OverrideEncoding(this AngleSharp.Io.VirtualResponse response, string? encoding)
        => response.Header("content-type", "text/html; charset=" + encoding);
}
