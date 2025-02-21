using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui;

public partial class MainPage : ContentPage
{
    public MainPage(JsonFileRepository<Venue> venueRepo, Scraper scraper)
    {
        var venueList = new VenueList(venueRepo, scraper, Navigation);

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
