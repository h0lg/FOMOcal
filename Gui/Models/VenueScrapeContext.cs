using System.Collections.Concurrent;

namespace FomoCal;

/// <summary>An Adapter unifying the APIs for scraping <see cref="Venue.ProgramUrl"/>
/// pages with or without automation.</summary>
public partial class VenueScrapeContext : IDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    internal readonly Venue Venue;
    private readonly IBrowser browser; // not ours to dispose, just holding on to it
    private readonly IAutomateAnEventListing? automator;
    private readonly ConcurrentBag<string> log = [];
    private readonly Action? cleanup;

    internal Task<IDomDocument?> Loading { get; private set; }

    internal VenueScrapeContext(Venue venue, IBrowser browser, IBuildEventListingAutomators automatorFactory)
    {
        Venue = venue;
        this.browser = browser;

        if (venue.Event.RequiresAutomation())
        {
            (automator, cleanup) = automatorFactory.BuildAutomator(this);
            Loading = automator.LoadAutomated(browser, venue, throwOnTimeout: true); // first page of events is required
        }
        else
            Loading = venue.TryGetDirectHtmlEncoding(out var encoding) ? LoadOverridingEncoding(encoding)
                : browser.OpenAsync(venue.ProgramUrl) as Task<IDomDocument?>;
    }

    internal async Task<IDomDocument?> LoadMoreAsync()
    {
        var loading = await browser.LoadMoreAsync(Venue, automator, Loading.Result!, Log);
        if (loading == null) return null;
        Loading.Result?.Dispose(); // dispose previous document
        Loading = loading;
        return await Loading;
    }

    /// <summary>Loads the <see cref="Venue.ProgramUrl"/> using the <see cref="Venue.Encoding"/> override
    /// to re-interpret the character set of the response.</summary>
    /// <returns>The <see cref="Loading"/> task.</returns>
    private async Task<IDomDocument?> LoadOverridingEncoding(string encoding)
    {
        using HttpClient httpClient = new();
        using var response = await httpClient.GetAsync(Venue.ProgramUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();

        return await browser.OpenAsync(response =>
            response.Content(stream).Address(Venue.ProgramUrl).OverrideEncoding(encoding));
    }

    internal void Log(string message, string? level = null) => log.Add($"{DateTime.UtcNow:o} {level ?? "INFO"} {message}");
    internal string GetScrapeLog() => log.Reverse().LineJoin();

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed) return;
        Loading.Result?.Dispose();
        Loading.Dispose();
        cleanup?.Invoke();
        isDisposed = true; // allow GC to clean up the rest
    }
}
