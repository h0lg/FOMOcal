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

    [ObservableProperty] public partial bool IsLoading { get; set; }

    public ObservableCollection<Venue> Venues { get; } = [];

    internal event Action<Venue, HashSet<Event>>? EventsScraped;
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
        catch (Exception ex)
        {
            await ErrorReport.WriteAsyncAndShare(ex.ToString(), "loading venues");
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
            venues ??= [.. Venues];
            Venues.Clear();

            foreach (var venue in venues.OrderByDescending(v => v.LastRefreshed))
                Venues.Add(venue);
        });

    [RelayCommand]
    private async Task AddVenue()
    {
        TaskCompletionSource<VenueEditor.Actions?> adding = new();

        Venue added = new()
        {
            Name = "",
            ProgramUrl = "",
            Event = new() { Selector = "", Name = new(), Date = new() }
        };

        VenueEditor model = new(added, scraper, adding);
        await navigation.PushAsync(new VenueEditor.Page(model));
        VenueEditor.Actions? result = await adding.Task; // wait for editor
        if (result == null) return; // canceled, do nothing

        switch (result)
        {
            case VenueEditor.Actions.Saved:
                Venues.Add(added);
                await SaveVenues();
                break;

            case VenueEditor.Actions.Deleted:
                break;
        }

        await navigation.PopAsync();
    }

    [RelayCommand]
    private async Task EditVenue(Venue original)
    {
        TaskCompletionSource<VenueEditor.Actions?> editing = new();
        Venue edited = original.DeepCopy(); // so that original is not changed by the editor
        VenueEditor model = new(edited, scraper, editing);
        await navigation.PushAsync(new VenueEditor.Page(model));
        VenueEditor.Actions? result = await editing.Task; // wait for editor
        if (result == null) return; // canceled, do nothing

        switch (result)
        {
            case VenueEditor.Actions.Saved:
                Venues.Remove(original);
                Venues.Add(edited);
                await SaveVenues();

                if (original.Name != edited.Name)
                    VenueRenamed?.Invoke(original.Name, edited.Name); // notify subscribers

                await LoadVenuesAsync(); // to refresh UI
                break;

            case VenueEditor.Actions.Deleted:
                Venues.Remove(original);
                VenueDeleted?.Invoke(original.Name); // notify subscribers
                await SaveVenues();
                break;
        }

        await navigation.PopAsync();
    }

    [RelayCommand]
    private async Task RefreshVenue(Venue venue)
    {
        var errors = await RefreshEvents(venue);
        RefreshList();
        await SaveVenues();
        if (errors.Length > 0) await WriteErrorReportAsync(ReportErrors(errors, venue));
    }

    [RelayCommand]
    private async Task RefreshAllVenues()
    {
        var refreshs = Venues.Select(venue => (venue, task: RefreshEvents(venue))).ToArray();
        await Task.WhenAll(refreshs.Select(r => r.task));
        RefreshList();
        await SaveVenues();

        var scrapesWithErrors = refreshs.Where(r => r.task.Result.Length > 0).ToArray();

        if (scrapesWithErrors.Length > 0)
        {
            string errorReport = scrapesWithErrors.Select(r => ReportErrors(r.task.Result, r.venue)).Join(ErrorReport.OutputSpacing);
            await WriteErrorReportAsync(errorReport);
        }
    }

    [RelayCommand]
    private void ExportVenues() => venueRepo.ShareFile("venues");

    [RelayCommand]
    private async Task ImportVenues()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Pick a venues config",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { { DevicePlatform.WinUI, [".json"] } })
        });

        if (file != null)
        {
            HashSet<Venue>? imported;

            try
            {
                imported = await JsonFileStore.DeserializeFrom<HashSet<Venue>>(file.FullPath);
            }
            catch (Exception ex)
            {
                await App.CurrentPage.DisplayAlert("Error importing venues", ex.Message, "Ok");
                return;
            }

            if (imported?.Count < 1) return;
            Venues.Import(imported!);
            await SaveVenues();
            RefreshList();
        }
    }

    private async Task<Exception[]> RefreshEvents(Venue venue)
    {
        (HashSet<Event> events, Exception[] errors) = await scraper.ScrapeVenueAsync(venue);
        venue.LastRefreshed = DateTime.Now;
        EventsScraped?.Invoke(venue, events); // notify subscribers
        return errors;
    }

    private Task SaveVenues() => venueRepo.SaveCompleteAsync(Venues.ToHashSet());

    private static string ReportErrors(Exception[] errors, Venue venue)
        => errors.Select(ex => ex.ToString())
            .Prepend("Scraping " + venue.Name + " " + venue.ProgramUrl)
            .Join(ErrorReport.OutputSpacing);

    private static async Task WriteErrorReportAsync(string errorReport)
        => await ErrorReport.WriteAsyncAndShare(errorReport, "refreshing venues");

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
                    var location = BndLbl(nameof(Venue.Location)).StyleClass(Styles.Label.VenueRowDetail);

                    var lastRefreshed = BndLbl(nameof(Venue.LastRefreshed), stringFormat: "last ⛏ {0:g}")
                        .StyleClass(Styles.Label.VenueRowDetail)
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

            var title = Lbl("🏟 Venues").StyleClass(Styles.Label.Headline).CenterVertical();
            var importVenues = Btn("📥", nameof(ImportVenuesCommand));
            var exportVenues = Btn("🥡", nameof(ExportVenuesCommand));
            var addVenue = Btn("➕", nameof(AddVenueCommand));
            var refreshAll = Btn("⛏ dig all gigs", nameof(RefreshAllVenuesCommand));

            Content = Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Star, Auto], spacing: 5,
                title.ColumnSpan(2), importVenues.Column(2), exportVenues.Column(3),
                list.Row(1).ColumnSpan(4),
                addVenue.Row(2), refreshAll.Row(2).Column(2).ColumnSpan(2));
        }
    }

    /// <summary>Wraps the <see cref="View"/> in a stand-alone Page for narrow devices that use AppShell.</summary>
    public partial class Page : ContentPage
    {
        public Page(JsonFileRepository<Venue> venueRepo, Scraper scraper, EventRepository eventRepo)
        {
            Title = "Venues";
            VenueList venueList = new(venueRepo, scraper, Navigation);
            venueList.EventsScraped += async (_, events) => await eventRepo.AddOrUpdateAsync(events);
            venueList.VenueRenamed += async (oldName, newName) => await eventRepo.RenameVenueAsync(oldName, newName);
            venueList.VenueDeleted += async (venueName) => await eventRepo.DeleteVenueAsync(venueName);
            Content = new View(venueList);
        }
    }
}
