using System.Collections.Concurrent;
using AngleSharp;
using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

/// <summary>An Adapter unifying the APIs for scraping <see cref="Venue.ProgramUrl"/>
/// pages with or without automation.</summary>
internal partial class VenueScrapeContext : IDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    internal readonly Venue Venue;
    private readonly IBrowsingContext browsingContext; // not ours to dispose, just holding on to it
    private readonly AutomatedEventPageView? loader;
    private readonly ConcurrentBag<string> log = [];
    private readonly Action? cleanup;

    internal Task<DomDoc?> Loading { get; private set; }

    /// <summary>For scraping a <paramref name="venue"/> that doesn't require automation.</summary>
    internal VenueScrapeContext(Venue venue, IBrowsingContext browsingContext)
    {
        Venue = venue;
        this.browsingContext = browsingContext;

        Loading = venue.TryGetDirectHtmlEncoding(out var encoding) ? LoadOverridingEncoding(encoding)
            : browsingContext.OpenAsync(venue.ProgramUrl) as Task<DomDoc?>;
    }

    /// <summary>For scraping a <paramref name="venue"/> that requires automation.
    /// Takes care of adding an <see cref="AutomatedEventPageView"/> to the <paramref name="layout"/>
    /// and removing it again on disposal.</summary>
    internal VenueScrapeContext(Venue venue, IBrowsingContext browsingContext, Layout layout)
    {
        Venue = venue;
        this.browsingContext = browsingContext;

        const int height = 1000, width = 1000;

        /* Add loader to an AbsoluteLayout that lets it have a decent size and be IsVisible
         * (which some pages require to properly scroll and load more events)
         * while staying out of view and not taking up space in the layout it's added to. */
        loader = new AutomatedEventPageView(venue, Log)
            //.LayoutBounds(0, 0, width, height) // use to see what's going on
            .LayoutBounds(-2 * width, -2 * height, width, height); // position off-screen with a decent size

        AbsoluteLayout wrapper = new() { WidthRequest = 0, HeightRequest = 0 };
        wrapper.Add(loader);
        layout.Add(wrapper); // to start the loader's life cycle

        Loading = loader.LoadAutomated(browsingContext, venue, throwOnTimeout: true); // first page of events is required
        cleanup = () => layout.Remove(wrapper); // make sure to remove loader again
    }

    internal async Task<DomDoc?> LoadMoreAsync()
    {
        var loading = await browsingContext.LoadMoreAsync(Venue, loader, Loading.Result!, Log);
        if (loading == null) return null;
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
        Loading.Dispose();
        cleanup?.Invoke();
        isDisposed = true; // allow GC to clean up the rest
    }
}
