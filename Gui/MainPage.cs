using CommunityToolkit.Maui.Markup;
using FomoCal.Gui.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui;

public partial class MainPage : ContentPage
{
    public MainPage(VenueList.View venueList)
    {
        Content = new Grid
        {
            ColumnDefinitions = Columns.Define(Auto),
            RowDefinitions = Rows.Define(Star), // Full height

            Children =
            {
                venueList.Column(0)
            }
        };
    }
}
