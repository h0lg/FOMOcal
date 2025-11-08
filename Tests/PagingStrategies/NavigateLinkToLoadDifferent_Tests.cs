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

    /* Sommerloch - during festival season, bands play festivals instead of venues
     * leaving only pages after pages of repeating parties in the programs of some venues  */
    [TestMethod]
    public async Task AllExcludedByFilterWontStopPaging()
    {
        browser.AddEvents(venue, 10, category: "boring party");
        browser.AddNextPageLink("#page-1");

        browser.AddEvents(venue, 10, category: "boring party", start: 11, page: 1);
        browser.AddNextPageLink("#page-2", page: 1);

        browser.AddEvents(venue, 10, start: 21, page: 2);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines(
            "found 10 events, 0 matched by concert",
            "next page link goes to #page-1",
            "found 10 events, 0 matched by concert",
            "next page link goes to #page-2",
            "found 10 events",
            "found 30 relevant events in total");

        Assert.HasCount(10, events);
    }

    /* some venues show only events in the current month on the first page
     * - which may all already be in the past when visited towards the end of the month */
    [TestMethod]
    public async Task TriesPagingAtLeastOnceIfCurrentPageContainsOnlyPastEvents()
    {
        browser.AddEvents(venue, 10, error: EventPageError.Past);
        browser.AddNextPageLink("#page-1");

        browser.AddEvents(venue, 10, start: 11, page: 1);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines(
            "found 10 events, 10 in the past - i.e. 10 irrelevant",
            "next page link goes to #page-1",
            "found 10 events",
            "found 10 relevant events in total");

        Assert.HasCount(10, events);
    }
}
