using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui;

public partial class MainPage : ContentPage
{
    public MainPage(JsonFileRepository<Venue> venueRepo, Scraper scraper, EventList eventList)
    {
        _ = eventList.LoadEvents();

        VenueList venueList = new(venueRepo, scraper, Navigation);
        venueList.EventsScraped += (venue, events) => eventList.RefreshWith(events);
        venueList.VenueRenamed += eventList.RenameVenue;
        venueList.VenueDeleted += eventList.DeleteForVenue;

        Content = new Grid
        {
            ColumnSpacing = 5,
            ColumnDefinitions = Columns.Define(Auto, Star),
            RowDefinitions = Rows.Define(Star), // Full height

            Children =
            {
                new VenueList.View(venueList).Width(250),
                new EventList.View(eventList).Column(1)
            }
        };
    }
}
