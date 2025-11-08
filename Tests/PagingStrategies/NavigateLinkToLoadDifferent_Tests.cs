using FomoCal;

namespace Tests.PagingStrategies;

[TestClass]
public sealed partial class NavigateLinkToLoadDifferent_Tests : PagingStrategyTests
{
    public NavigateLinkToLoadDifferent_Tests() : base(Venue.PagingStrategy.NavigateLinkToLoadDifferent) { }

    [TestMethod]
    public async Task AllScrapable()
    {
        browser.AddEvents(venue, 10);
        browser.AddNextPageLink("#page-1");

        browser.AddEvents(venue, 10, start: 11, page: 1);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines("paging strategy loads different by navigating link .next-page",
            "found 10 events",
            "next page link goes to #page-1",
            "found 10 events",
            "found 20 relevant events in total");

        Assert.HasCount(20, events);
    }
}
