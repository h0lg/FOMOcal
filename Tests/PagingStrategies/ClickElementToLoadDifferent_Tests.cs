using FomoCal;

namespace Tests.PagingStrategies;

[TestClass]
public sealed partial class ClickElementToLoadDifferent_Tests : PagingStrategyTests
{
    public ClickElementToLoadDifferent_Tests() : base(Venue.PagingStrategy.ClickElementToLoadDifferent) { }

    [TestMethod]
    public async Task PagingStopsWhenThereIsNoNextPageElement()
    {
        browser.AddEvents(venue, 10);
        browser.AddNextPageButton();
        browser.AddEvents(venue, 10, start: 11, page: 1);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines("paging strategy loads different by clicking .next-page",
            "found 10 events",
            "found 10 events",
            "found 20 relevant events in total");

        Assert.HasCount(20, events);
    }

    [TestMethod]
    public async Task PagingStopsWhenEventsDo()
    {
        browser.AddEvents(venue, 10);
        browser.AddNextPageButton();

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines(
            "found 10 events",
            "can load more",
            "found 0 events",
            "found 10 relevant events in total");

        Assert.HasCount(10, events);
    }
}
