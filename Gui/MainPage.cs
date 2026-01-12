using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui;

public partial class MainPage : ContentPage
{
    public MainPage(VenueCollection venues, EventRepository eventRepo)
    {
        EventList eventList = new(eventRepo, venues, Navigation);
        VenueList venueList = new(Navigation, venues);
        venues.EventsScraped += eventList.RefreshWith;
        venues.Renamed += eventList.RenameVenue;
        venues.Deleted += eventList.DeleteForVenue;

        Content = Grd(cols: [Auto, Star], rows: [Star], spacing: 5,
            new VenueList.View(venueList).Width(250),
            new EventList.View(eventList).Column(1));

        var loaded = false;

        NavigatedTo += async (o, e) =>
        {
            if (loaded) return;
            loaded = true;
            await Task.WhenAll(venues.LoadAsync(), eventList.LoadEvents());
        };
    }
}
