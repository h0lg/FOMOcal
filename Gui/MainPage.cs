using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui;

public partial class MainPage : ContentPage
{
    public MainPage(JsonFileRepository<Venue> venueRepo, Scraper scraper, JsonFileRepository<Event> eventRepo)
    {
        VenueList venueList = new(venueRepo, scraper, Navigation);
        venueList.EventsScraped += async (venue, events) => await eventRepo.AddOrUpdateAsync(events);

        Content = new Grid
        {
            ColumnDefinitions = Columns.Define(Auto),
            RowDefinitions = Rows.Define(Star), // Full height

            Children =
            {
                new VenueList.View(venueList)
            }
        };
    }
}
