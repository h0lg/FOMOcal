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

    internal event Action<Venue, List<Event>>? EventsScraped;
    internal event Action<string, string>? VenueRenamed;
    internal event Action<string>? VenueDeleted;

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
            RefreshList(venues);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshList(IEnumerable<Venue>? venues = null) =>
        // Ensure UI updates on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            venues ??= Venues.ToArray();
            Venues.Clear();

            foreach (var venue in venues.OrderByDescending(v => v.LastRefreshed))
                Venues.Add(venue);
        });

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

                if (result.OriginalVenueName != venue.Name)
                    VenueRenamed?.Invoke(result.OriginalVenueName!, venue.Name); // notify subscribers

                await LoadVenuesAsync(); // to refresh UI
                break;

            case VenueEditor.Result.Actions.Deleted:
                Venues.Remove(result.Venue);
                VenueDeleted?.Invoke(result.OriginalVenueName!); // notify subscribers
                await SaveVenues();
                break;
        }

        await navigation.PopAsync();
    }

    [RelayCommand]
    private async Task RefreshVenue(Venue venue)
    {
        await RefreshEvents(venue);
        RefreshList();
        await SaveVenues();
    }

    [RelayCommand]
    private async Task RefreshAllVenues()
    {
        await Task.WhenAll(Venues.Select(RefreshEvents));
        RefreshList();
        await SaveVenues();
    }

    [RelayCommand]
    private async Task ExportVenues() => await venueRepo.ShareFile("venues");

    private async Task RefreshEvents(Venue venue)
    {
        var events = await scraper.ScrapeVenueAsync(venue);
        venue.LastRefreshed = DateTime.Now;
        EventsScraped?.Invoke(venue, events); // notify subscribers
    }

    private Task SaveVenues() => venueRepo.SaveCompleteAsync(Venues.ToHashSet());

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

                    var lastRefreshed = BndLbl(nameof(Venue.LastRefreshed), stringFormat: "last ⛏ {0:g}")
                        .FontSize(12).TextColor(Colors.Gray)
                        .Bind(IsVisibleProperty, getter: static (Venue v) => v.LastRefreshed.HasValue);

                    var refresh = Btn("⛏", nameof(RefreshVenueCommand), source: model);

                    return new Border
                    {
                        Padding = 10,
                        Content = Grd(cols: [Star, Auto], rows: [Auto, Auto, Auto], spacing: 5,
                            name.ColumnSpan(2),
                            location.Row(1),
                            refresh.Row(1).Column(1).RowSpan(2).Bottom(),
                            lastRefreshed.Row(2).End())
                    }.BindTapGesture(nameof(EditVenueCommand), commandSource: model, parameterPath: ".");
                }));

            var title = Lbl("🏛 Venues").Bold().FontSize(20).CenterVertical();
            var exportVenues = Btn("🥡", nameof(ExportVenuesCommand));
            var addVenue = Btn("➕", nameof(AddVenueCommand));
            var refreshAll = Btn("⛏ dig all gigs", nameof(RefreshAllVenuesCommand));

            Content = Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Star, Auto], spacing: 5,
                title.ColumnSpan(3), exportVenues.Column(3),
                list.Row(1).ColumnSpan(4),
                addVenue.Row(2), refreshAll.Row(2).Column(2).ColumnSpan(2));
        }
    }

    public partial class Page : ContentPage
    {
        public Page(JsonFileRepository<Venue> venueRepo, Scraper scraper, EventRepository eventRepo)
        {
            Title = "Venues";
            VenueList venueList = new(venueRepo, scraper, Navigation);
            venueList.EventsScraped += async (venue, events) => await eventRepo.AddOrUpdateAsync(events);
            venueList.VenueRenamed += async (oldName, newName) => await eventRepo.RenameVenueAsync(oldName, newName);
            venueList.VenueDeleted += async (venueName) => await eventRepo.DeleteVenueAsync(venueName);
            Content = new View(venueList);
        }
    }
}
