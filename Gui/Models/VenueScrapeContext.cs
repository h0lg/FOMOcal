using System.Collections.Concurrent;

namespace FomoCal;

/// <summary>An Adapter unifying the APIs for scraping <see cref="Venue.ProgramUrl"/>
/// pages with or without automation.</summary>
public sealed partial class VenueScrapeContext : IDisposable, IAsyncDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    public readonly Venue Venue;
    private readonly IBrowser browser; // not owned
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
            Loading = automator.LoadAutomated(browser, venue);
            automator.Url = venue.ProgramUrl;
        }
        else
            Loading = venue.TryGetDirectHtmlEncoding(out var encoding) ? LoadOverridingEncoding(encoding)
                : browser.OpenAsync(venue.ProgramUrl) as Task<IDomDocument?>;
    }

    internal async Task<IDomDocument?> LoadMoreAsync()
    {
        ThrowIfDisposed();
        var next = await browser.LoadMoreAsync(Venue, automator, Loading.Result!, Log);
        if (next == null) return null;
        await DisposeDocumentAsync(Loading.Result); // dispose previous document
        Loading = next;
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

    public void Log(string message, string? level = null) => log.Add(FormatLog(message, level));

    internal static string FormatLog(string message, string? level)
        => $"{DateTime.UtcNow:o} {level ?? "INFO"} {message}";

    internal string GetScrapeLog() => log.Reverse().LineJoin();

    #region disposing
    private int disposed; // 0 = false, 1 = true

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        if (Loading.IsCompletedSuccessfully)  // avoid throwing exception if task failed
            await DisposeDocumentAsync(Loading.Result);

        cleanup?.Invoke();
    }

    private static async ValueTask DisposeDocumentAsync(IDomDocument? document)
    {
        if (document is null) return;

        if (document is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else document.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, typeof(VenueScrapeContext));
    #endregion
}
