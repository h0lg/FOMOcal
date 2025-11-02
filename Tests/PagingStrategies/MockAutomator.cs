using FomoCal;

namespace Tests.PagingStrategies;

class MockAutomator : IAutomateAnEventListing
{
    public string? Url { get; set; }

    public event Action<string?>? HtmlWithEventsLoaded;
    public event Action<WebNavigationResult>? ErrorLoading;

    public Task ClickElementToLoadDifferent(string selector) => throw new NotImplementedException();
    public Task ClickElementToLoadMore(string selector) => throw new NotImplementedException();
    public Task ScrollDownToLoadMore() => throw new NotImplementedException();
}

class MockAutomatorFactory : IBuildEventListingAutomators
{
    public (IAutomateAnEventListing automator, Action? cleanup) BuildAutomator(VenueScrapeContext venueScrape)
        => (new MockAutomator(), null);
}

public class MockScrapeLogFileSaver : ISaveScrapeLogFiles
{
    internal string? Log { get; private set; }

    public Task<string?> SaveScrapeLogAsync(Venue venue, string log)
    {
        Log = log;
        return Task.FromResult<string?>(null);
    }
}
