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

    internal async Task<DomDoc> GetDocumentAsync(Venue venue)
        => await context.OpenAsync(venue.ProgramUrl);

    public void Dispose() => context.Dispose();
}

internal static class ScraperExtensions
{
    internal static IEnumerable<AngleSharp.Dom.IElement> SelectEvents(this DomDoc document, Venue venue)
        => document.QuerySelectorAll(venue.Event.Selector);
}
