using AngleSharp;
using FomoCal;

namespace Tests.PagingStrategies;

class MockAutomator(VenueScrapeContext venueScrape, MockBrowser browser) : IAutomateAnEventListing
{
    private string? url;

    public string? Url
    {
        get => url;
        set
        {
            url = value;
            SimulateHtmlLoadedAsync().GetAwaiter().GetResult();
        }
    }

    public event Action<string?>? HtmlWithEventsLoaded;
    public event Action<WebNavigationResult>? ErrorLoading;

    public Task ClickElementToLoadDifferent(string selector) => SimulateHtmlLoadedAsync();
    public Task ClickElementToLoadMore(string selector) => SimulateHtmlLoadedAsync();
    public Task ScrollDownToLoadMore() => SimulateHtmlLoadedAsync();

    private async Task SimulateHtmlLoadedAsync()
    {
        try
        {
            var doc = await browser.CreateDocumentAsync();
            HtmlWithEventsLoaded?.Invoke(doc.ToHtml());
        }
        catch (Exception ex)
        {
            venueScrape.Log(ex.ToString(), "ERROR");
            ErrorLoading?.Invoke(WebNavigationResult.Failure);
        }
    }
}

class MockAutomatorFactory(MockBrowser browser) : IBuildEventListingAutomators
{
    public (IAutomateAnEventListing automator, Action? cleanup) BuildAutomator(VenueScrapeContext venueScrape)
        => (new MockAutomator(venueScrape, browser), null);
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
