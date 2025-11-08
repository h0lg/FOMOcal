using FomoCal;

namespace Tests.PagingStrategies;

[TestClass]
public sealed partial class AllOnOnePage_Tests : PagingStrategyTests
{
    public AllOnOnePage_Tests() : base(Venue.PagingStrategy.AllOnOne) { }

    [TestMethod]
    public async Task AllScrapable()
    {
        browser.AddEvents(venue, 10);
        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines("paging strategy loads all on the first page",
            "found 10 events",
            "found 10 relevant events in total");

        Assert.HasCount(10, events);
    }

    [TestMethod]
    public async Task HalfMissingRequiredInfo()
    {
        browser.AddEvents(venue, 2);
        browser.AddEvents(venue, 2, error: EventPageError.NoDate);
        browser.AddEvents(venue, 3);
        browser.AddEvents(venue, 3, error: EventPageError.NoDate);
        browser.AddEvents(venue, 5);
        browser.AddEvents(venue, 5, error: EventPageError.NoName);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines(
            "found 20 events, 10 of them in the past or unscrapable",
            "found 10 relevant events in total");

        Assert.HasCount(10, events);
    }

    [TestMethod]
    public async Task HalfInThePast()
    {
        browser.AddEvents(venue, 5);
        browser.AddEvents(venue, 5, error: EventPageError.Past);
        browser.AddEvents(venue, 5);
        browser.AddEvents(venue, 5, error: EventPageError.Past);

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines(
            "found 20 events, 10 of them in the past or unscrapable",
            "found 10 relevant events in total");

        Assert.HasCount(10, events);
    }

    [TestMethod]
    public async Task HalfExcludedByFilter()
    {
        browser.AddEvents(venue, 10);
        browser.AddEvents(venue, 10, category: "boring party");

        (var events, var errors) = await scraper.ScrapeVenueAsync(venue);
        AssertEmpty(errors);

        AssertLogLines(
            "found 20 events, 10 matched by concert",
            "found 20 relevant events in total");

        Assert.HasCount(10, events);
    }
}
