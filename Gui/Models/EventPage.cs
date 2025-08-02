using AngleSharp;
using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using Microsoft.Maui.Layouts;
using DomDoc = AngleSharp.Dom.IDocument;

namespace FomoCal;

internal partial class EventPage : IDisposable // to support custom cleanup in order to detach the loader from the layout again
{
    private readonly Venue venue;
    private readonly IBrowsingContext browsingContext; // not ours to dispose, just holding on to it
    private readonly AutomatedEventPageView? loader;
    private readonly Action? cleanup;

    internal Task<DomDoc?> Loading { get; private set; }

    internal EventPage(Venue venue, IBrowsingContext browsingContext)
    {
        this.venue = venue;
        this.browsingContext = browsingContext;

        Loading = venue.TryGetDirectHtmlEncoding(out var encoding) ? LoadOverridingEncoding(encoding)
            : browsingContext.OpenAsync(venue.ProgramUrl) as Task<DomDoc?>;
    }

    internal EventPage(Venue venue, IBrowsingContext browsingContext, Layout layout)
    {
        this.venue = venue;
        this.browsingContext = browsingContext;

        const int height = 1000, width = 1000;

        /* Add loader to an AbsoluteLayout that lets it have a decent size and be IsVisible
         * (which some pages require to properly scroll and load more events)
         * while staying out of view and not taking up space in the layout it's added to. */
        loader = new AutomatedEventPageView(venue)
            //.LayoutBounds(0, 0, width, height) // use to see what's going on
            .LayoutBounds(-2 * width, -2 * height, width, height) // position off-screen with a decent size
            .LayoutFlags(AbsoluteLayoutFlags.None);

        AbsoluteLayout wrapper = new() { WidthRequest = 0, HeightRequest = 0 };
        wrapper.Add(loader);
        layout.Add(wrapper); // to start the loader's life cycle

        Loading = loader.LoadAutomated(browsingContext, venue, throwOnTimeout: true); // first page of events is required
        cleanup = () => layout.Remove(wrapper); // make sure to remove loader again
    }

    internal async Task<DomDoc?> LoadMoreAsync()
    {
        var loading = await browsingContext.LoadMoreAsync(venue, loader, Loading.Result!);
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
        using var response = await httpClient.GetAsync(venue.ProgramUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();

        return await browsingContext.OpenAsync(response =>
            response.Content(stream).Address(venue.ProgramUrl).OverrideEncoding(encoding));
    }

    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed) return;
        Loading.Dispose();
        cleanup?.Invoke();
        isDisposed = true; // allow GC to clean up the rest
    }
}
