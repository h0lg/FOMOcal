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

    public event Action<string?>? HtmlLoaded;
    public event Action<WebNavigationResult>? ErrorLoading;

    public Task ClickElementToLoadDifferent(string selector) => SimulateHtmlLoadedAsync();
    public Task ClickElementToLoadMore(string selector) => SimulateHtmlLoadedAsync();
    public Task ScrollDownToLoadMore() => SimulateHtmlLoadedAsync();

    private async Task SimulateHtmlLoadedAsync()
    {
        try
        {
            var eventPage = browser.GetCurrentEventPage();
            string? html = null;

            if (eventPage == null /* none loaded yet */
                // either paging requires no next page selector
                || !venueScrape.Venue.Event.PagingStrategy.RequiresNextPageSelector()
                // or there is one
                || eventPage.AddNextPageNavigator != null)
            {
                var doc = await browser.CreateDocumentAsync();
                html = doc.ToHtml();
            }
            /* Otherwise, return null html. This simulates the script behavior
             * when notifyFound(false) is returned in case the NextPageSelector matches no element.
             * That should never occur though - CanLoadMore(this IDomDocument document, Venue venue)
             * stops the paging in that scenario. */

            HtmlLoaded?.Invoke(html);
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
