using FomoCal;

namespace Tests.PagingStrategies;

[TestClass]
public sealed partial class ScrollDownToLoadMore_Tests : PagingStrategyTests
{
    public ScrollDownToLoadMore_Tests() : base(Venue.PagingStrategy.ScrollDownToLoadMore) { }

    [TestMethod]
    public async Task PagingStopsWhenEventsDo()
    {
        browser.AddEvents(venue, 10);
        browser.AddEvents(venue, 10, start: 11, page: 1);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines("paging strategy loads more by scrolling down",
            "selected 10 events",
            "selected 20 events -10 already scraped",
            "selected 20 events -20 already scraped",
            "scraped 20 events in total");

        Assert.HasCount(20, events);
    }
}
