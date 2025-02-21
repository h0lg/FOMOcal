using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueList : ObservableObject
{
    private readonly JsonFileRepository<Venue> venueRepo;
    private readonly Scraper scraper;
    private readonly INavigation navigation;

    [ObservableProperty] private bool isLoading;

    public ObservableCollection<Venue> Venues { get; } = [];

    public VenueList(JsonFileRepository<Venue> venueRepo, Scraper scraper, INavigation navigation)
    {
        this.venueRepo = venueRepo;
        this.scraper = scraper;
        this.navigation = navigation;
        _ = LoadVenuesAsync(); // Fire & forget (no need to await in constructor)
    }

    private async Task LoadVenuesAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var venues = await venueRepo.LoadAllAsync();

            // Ensure UI updates on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Venues.Clear();

                foreach (var venue in venues.OrderByDescending(v => v.LastRefreshed))
                    Venues.Add(venue);
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddVenue()
    {
        var model = new VenueEditor(new Venue
        {
            Name = "",
            ProgramUrl = "",
            Event = new() { Selector = "", Name = new(), Date = new() }
        }, scraper, venueRepo);

        await navigation.PushAsync(new VenueEditor.Page(model));
    }

    [RelayCommand]
    private async Task EditVenue(Venue venue)
    {
        var model = new VenueEditor(venue, scraper, venueRepo);
        await navigation.PushAsync(new VenueEditor.Page(model));
    }

    public partial class View : ContentView
    {
        public View(VenueList model)
        {
            BindingContext = model;

            var list = new CollectionView()
                .Bind(ItemsView.ItemsSourceProperty, nameof(Venues))
                .ItemTemplate(new DataTemplate(() =>
                {
                    var name = BndLbl(nameof(Venue.Name)).FontSize(16).Wrap();
                    var location = BndLbl(nameof(Venue.Location)).FontSize(12).TextColor(Colors.Gray).Wrap();

                    var edit = Btn("✏️", nameof(EditVenueCommand), source: model);

                    return new Border
                    {
                        Padding = 10,
                        Content = new StackLayout
                        {
                            Spacing = 5,
                            Children = { name, location, edit }
                        }
                    };
                }));

            var addVenue = Btn("➕ Add Venue", nameof(AddVenueCommand));

            Content = new Grid
            {
                RowSpacing = 10,
                ColumnDefinitions = Columns.Define(Star),
                RowDefinitions = Rows.Define(Star, Auto),
                Children = { list, addVenue.Row(1) }
            };
        }
    }

    public partial class Page : ContentPage
    {
        public Page(JsonFileRepository<Venue> venueRepo, Scraper scraper)
        {
            Title = "Venues";
            Content = new View(new VenueList(venueRepo, scraper, Navigation));
        }
    }
}
