using AngleSharp.XPath;
using DomDoc = AngleSharp.Dom.IDocument;
using DomElmt = AngleSharp.Dom.IElement;

namespace FomoCal;

internal static class ScraperExtensions
{
    internal static IEnumerable<DomElmt> SelectEvents(this DomDoc document, Venue venue)
        => ScrapeJob.TryGetXPathSelector(venue.Event.Selector, out var xPathSelector)
            // see https://github.com/AngleSharp/AngleSharp.XPath
            ? document.Body.SelectNodes(xPathSelector).OfType<DomElmt>()
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
