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
        TaskCompletionSource<VenueEditor.Result?> adding = new();
        VenueEditor model = new(null, scraper, adding);
        await navigation.PushAsync(new VenueEditor.Page(model));
        VenueEditor.Result? result = await adding.Task; // wait for editor
        if (result == null) return; // canceled, do nothing

        switch (result.Action)
        {
            case VenueEditor.Result.Actions.Saved:
                Venues.Add(result.Venue);
                await SaveVenues();
                break;

            case VenueEditor.Result.Actions.Deleted:
                break;
        }

        await navigation.PopAsync();
    }

    [RelayCommand]
    private async Task EditVenue(Venue venue)
    {
        TaskCompletionSource<VenueEditor.Result?> editing = new();
        VenueEditor model = new(venue, scraper, editing);
        await navigation.PushAsync(new VenueEditor.Page(model));
        VenueEditor.Result? result = await editing.Task; // wait for editor
        if (result == null) return; // canceled, do nothing

        switch (result.Action)
        {
            case VenueEditor.Result.Actions.Saved:
                await SaveVenues(); // since we passed the venue by reference, it's already updated
                await LoadVenuesAsync(); // to refresh UI
                break;

            case VenueEditor.Result.Actions.Deleted:
                Venues.Remove(result.Venue);
                await SaveVenues();
                break;
        }

        await navigation.PopAsync();
    }

    private Task SaveVenues() => venueRepo.SaveAllAsync(Venues.ToHashSet());

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


                    return new Border
                    {
                        Padding = 10,
                        Content = new StackLayout
                        {
                            Spacing = 5,
                            Children = { name, location }
                        }
                    }.BindTapGesture(nameof(EditVenueCommand), commandSource: model, parameterPath: ".");
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
