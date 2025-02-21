using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FomoCal.Gui.ViewModels;

public partial class VenueList : ObservableObject
{
    private readonly JsonFileRepository<Venue> venueRepo;

    [ObservableProperty] private bool isLoading;

    public ObservableCollection<Venue> Venues { get; } = [];

    public VenueList(JsonFileRepository<Venue> venueRepo)
    {
        this.venueRepo = venueRepo;
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

                foreach (var venue in venues)
                {
                    Venues.Add(venue);
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
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
                    var name = new Label().FontSize(16).Wrap()
                        .Bind(Label.TextProperty, nameof(Venue.Name));

                    var location = new Label().FontSize(12).TextColor(Colors.Gray).Wrap()
                        .Bind(Label.TextProperty, nameof(Venue.Location));

                    return new Border
                    {
                        Padding = 10,
                        Content = new StackLayout
                        {
                            Spacing = 5,
                            Children = { name, location }
                        }
                    };
                }));

            Content = list;
        }
    }

    public partial class Page : ContentPage
    {
        public Page(View venueList)
        {
            Title = "Venues";
            Content = venueList;
        }
    }
}
