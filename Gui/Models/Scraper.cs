using PuppeteerSharp;

namespace FomoCal;

public class Scraper
{
    private readonly Task downloadingBrowser;
    private PuppeteerSharp.IBrowser? browser;

    internal bool IsReady { get; private set; }
    internal EventHandler Ready;

    public Scraper() => downloadingBrowser = DownloadBrowser(); // start download

    public async Task<List<Event>> ScrapeVenueAsync(Venue venue)
    {
        var events = new List<Event>();

        try
        {
            var programPage = await GetProgramPageAsync(venue);

            foreach (var eventElement in await programPage.SelectEventsAsync(venue))
            {
                var name = await venue.Event.Name.GetValueAsync(eventElement);
                DateTime? date = await venue.Event.Date.GetDateAsync(eventElement);
                if (name == null || date == null) continue;

                var scrapedEvent = new Event
                {
                    Venue = venue.Name,
                    Name = name,
                    Date = date.Value,
                    Scraped = DateTime.Now,
                    SubTitle = venue.Event.SubTitle == null ? null : await venue.Event.SubTitle.GetValueAsync(eventElement),
                    Description = venue.Event.Description == null ? null : await venue.Event.Description.GetValueAsync(eventElement),
                    Genres = venue.Event.Genres == null ? null : await venue.Event.Genres.GetValueAsync(eventElement),
                    Stage = venue.Event.Stage == null ? null : await venue.Event.Stage.GetValueAsync(eventElement),
                    DoorsTime = venue.Event.DoorsTime == null ? null : await venue.Event.DoorsTime.GetValueAsync(eventElement),
                    StartTime = venue.Event.StartTime == null ? null : await venue.Event.StartTime.GetValueAsync(eventElement),
                    PresalePrice = venue.Event.PresalePrice == null ? null : await venue.Event.PresalePrice.GetValueAsync(eventElement),
                    DoorsPrice = venue.Event.DoorsPrice == null ? null : await venue.Event.DoorsPrice.GetValueAsync(eventElement),
                    Url = venue.Event.Url == null ? null : await venue.Event.Url.GetUrlAsync(eventElement),
                    ImageUrl = venue.Event.ImageUrl == null ? null : await venue.Event.ImageUrl.GetUrlAsync(eventElement),
                    TicketUrl = venue.Event.TicketUrl == null ? null : await venue.Event.TicketUrl.GetUrlAsync(eventElement)
                };

                events.Add(scrapedEvent);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to scrape venue {venue.Name}: {ex.Message}");
        }

        return events;
    }

    internal async Task<IPage> GetProgramPageAsync(Venue venue)
    {
        var browsr = await GetBrowser();
        var page = await browsr.NewPageAsync();
        await page.GoToAsync(venue.ProgramUrl);
        return page;
    }

    private async ValueTask<PuppeteerSharp.IBrowser> GetBrowser()
    {
        if (browser != null) return browser;
        await downloadingBrowser;
        browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        return browser;
    }

    private async Task DownloadBrowser()
    {
        await new BrowserFetcher().DownloadAsync();
        IsReady = true;
        Ready?.Invoke(this, EventArgs.Empty);
    }
}

internal static class ScraperExtensions
{
    internal static async Task<IElementHandle[]> SelectEventsAsync(this IPage document, Venue venue)
        => await document.QuerySelectorAllAsync(venue.Event.Selector);

    internal static Task<string> GetTextContentAsync(this IElementHandle node)
        => node.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
}
