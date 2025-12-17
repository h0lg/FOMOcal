using CommunityToolkit.Maui.Markup;
using FomoCal.Gui;
using FomoCal.Gui.ViewModels;

namespace FomoCal;

public interface IAutomateAnEventListing
{
    /// <summary>An event that notifies the subscriber about the DOM from <see cref="Venue.ProgramUrl"/>
    /// being ready for scraping and returning its HTML - or null if events are
    /// <see cref="Venue.EventScrapeJob.LazyLoaded"/> and that times out.</summary>
    event Action<string?>? HtmlWithEventsLoaded;

    /// <summary>An event that notifies the subscriber about an error loading the <see cref="Venue.ProgramUrl"/>.</summary>
    event Action<WebNavigationResult>? ErrorLoading;

    string? Url { get; set; }

    Task ClickElementToLoadMore(string selector);
    Task ScrollDownToLoadMore();
    Task ClickElementToLoadDifferent(string selector);
}

public interface IBuildEventListingAutomators
{
    (IAutomateAnEventListing automator, Action? cleanup) BuildAutomator(VenueScrapeContext venueScrape);
}

public sealed class MauiEventListingAutomatorFactory : IBuildEventListingAutomators
{
    private Layout? topLayout;

    private Layout TopLayout
    {
        get
        {
            if (topLayout == null)
            {
                topLayout = App.GetCurrentContentPage().FindTopLayout() as Layout;

                if (topLayout == null) throw new InvalidOperationException(
                    $"You need to use the {nameof(Scraper)} on a {nameof(ContentPage)} with a {nameof(Layout)} to attach the {nameof(AutomatedEventPageView)} to.");
            }

            return topLayout!;
        }
    }

    public (IAutomateAnEventListing automator, Action? cleanup) BuildAutomator(VenueScrapeContext venueScrape)
    {
        const int height = 1000, width = 1000;

        /* Add loader to an AbsoluteLayout that lets it have a decent size and be IsVisible
         * (which some pages require to properly scroll and load more events)
         * while staying out of view and not taking up space in the layout it's added to. */
        var automator = new AutomatedEventPageView(venueScrape.Venue, venueScrape.Log)
            //.LayoutBounds(0, 0, width, height) // use to see what's going on
            .LayoutBounds(-2 * width, -2 * height, width, height); // position off-screen with a decent size

        AbsoluteLayout wrapper = new() { WidthRequest = 0, HeightRequest = 0 };
        wrapper.Add(automator);
        TopLayout.Add(wrapper); // to start the loader's life cycle
        return (automator, Cleanup);

        void Cleanup() => TopLayout.Remove(wrapper); // make sure to remove loader again
    }
}
