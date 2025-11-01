namespace FomoCal;

public interface IAutomateAnEventListing
{
    /// <summary>An event that notifies the subscriber about the DOM from <see cref="Venue.ProgramUrl"/>
    /// being ready for scraping and returning its HTML - or null if events are
    /// <see cref="Venue.EventScrapeJob.LazyLoaded"/> and that times out.</summary>
    event Action<string?>? HtmlWithEventsLoaded;

    /// <summary>An event that notifies the subscriber about an error loading the <see cref="Venue.ProgramUrl"/>.</summary>
    event Action<WebNavigationResult>? ErrorLoading;

    string? Url { get; set; }

    Task ClickElementToLoadMore(string selector);
    Task ScrollDownToLoadMore();
    Task ClickElementToLoadDifferent(string selector);
}
