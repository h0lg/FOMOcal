using FomoCal;

namespace Tests.PagingStrategies;

[TestClass]
public sealed partial class ClickElementToLoadMore_Tests : PagingStrategyTests
{
    public ClickElementToLoadMore_Tests() : base(Venue.PagingStrategy.ClickElementToLoadMore) { }

    [TestMethod]
    public async Task PagingStopsWhenThereIsNoNextPageElement()
    {
        browser.AddEvents(venue, 10);
        browser.AddNextPageButton();
        browser.AddEvents(venue, 10, start: 11, page: 1);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines("paging strategy loads more by clicking .next-page",
            "selected 10 events",
            "selected 20 events -10 already scraped",
            "scraped 20 events in total");

        Assert.HasCount(20, events);
    }
}
