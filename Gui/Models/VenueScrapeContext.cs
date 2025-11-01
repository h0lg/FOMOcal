using System.Collections.Concurrent;
using AngleSharp;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

/// <summary>An Adapter unifying the APIs for scraping <see cref="Venue.ProgramUrl"/>
/// pages with or without automation.</summary>
public partial class VenueScrapeContext : IDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    internal readonly Venue Venue;
    private readonly IBrowsingContext browsingContext; // not ours to dispose, just holding on to it
    private readonly IAutomateAnEventListing? automator;
    private readonly ConcurrentBag<string> log = [];
    private readonly Action? cleanup;

    internal Task<DomDoc?> Loading { get; private set; }

    internal VenueScrapeContext(Venue venue, IBrowsingContext browsingContext, IBuildEventListingAutomators automatorFactory)
    {
        Venue = venue;
        this.browsingContext = browsingContext;

        if (venue.Event.RequiresAutomation())
        {
            (automator, cleanup) = automatorFactory.BuildAutomator(this);
            Loading = automator.LoadAutomated(browsingContext, venue, throwOnTimeout: true); // first page of events is required
        }
        else
            Loading = venue.TryGetDirectHtmlEncoding(out var encoding) ? LoadOverridingEncoding(encoding)
                : browsingContext.OpenAsync(venue.ProgramUrl) as Task<DomDoc?>;
    }

    internal async Task<DomDoc?> LoadMoreAsync()
    {
        var loading = await browsingContext.LoadMoreAsync(Venue, automator, Loading.Result!, Log);
        if (loading == null) return null;
        Loading.Result?.Dispose(); // dispose previous document
        Loading = loading;
        return await Loading;
    }

    /// <summary>Loads the <see cref="Venue.ProgramUrl"/> using the <see cref="Venue.Encoding"/> override
    /// to re-interpret the character set of the response.</summary>
    /// <returns>The <see cref="Loading"/> task.</returns>
    private async Task<DomDoc?> LoadOverridingEncoding(string encoding)
    {
        using HttpClient httpClient = new();
        using var response = await httpClient.GetAsync(Venue.ProgramUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();

        return await browsingContext.OpenAsync(response =>
            response.Content(stream).Address(Venue.ProgramUrl).OverrideEncoding(encoding));
    }

    internal void Log(string message, string? level = null) => log.Add($"{DateTime.UtcNow:o} {level ?? "INFO"} {message}");
    internal string GetScrapeLog() => log.Reverse().LineJoin();
    internal Task<string?> SaveScrapeLogAsync() => ScrapeLogFile.Save(Venue, GetScrapeLog());

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
