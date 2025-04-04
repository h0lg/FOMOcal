using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui;

public partial class MainPage : ContentPage
{
    public MainPage(JsonFileRepository<Venue> venueRepo, Scraper scraper, EventList eventList)
    {
        _ = eventList.LoadEvents().ContinueWith(async t =>
        {
            if (t.Exception != null) await ErrorReport.WriteAsyncAndShare(t.Exception.ToString(), "loading events");
        });

        VenueList venueList = new(venueRepo, scraper, Navigation);
        venueList.EventsScraped += (venue, events) => eventList.RefreshWith(events);
        venueList.VenueRenamed += eventList.RenameVenue;
        venueList.VenueDeleted += eventList.DeleteForVenue;

        Content = Grd(cols: [Auto, Star], rows: [Star], spacing: 5,
            new VenueList.View(venueList).Width(250),
            new EventList.View(eventList).Column(1));
    }
}
