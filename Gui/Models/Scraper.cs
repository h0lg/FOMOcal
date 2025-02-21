using AngleSharp;

namespace FomoCal;

public class Scraper
{
    private readonly IBrowsingContext _context;

    public Scraper()
    {
        var config = Configuration.Default.WithDefaultLoader();
        _context = BrowsingContext.New(config);
    }

    internal async Task<AngleSharp.Dom.IDocument> GetDocument(Venue venue)
        => await _context.OpenAsync(venue.ProgramUrl);
}

internal static class ScraperExtensions
{
    internal static IEnumerable<AngleSharp.Dom.IElement> SelectEvents(this AngleSharp.Dom.IDocument document, Venue venue)
        => document.QuerySelectorAll(venue.Event.Selector);
}
