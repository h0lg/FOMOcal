using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueList : ObservableObject
{
    private readonly SetJsonFileRepository<Venue> venueRepo;
    private readonly Scraper scraper;
    private readonly INavigation navigation;
    private readonly HashSet<Venue> refreshingVenues = [];

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial double RefreshAllVenuesProgress { get; set; } = 1; // none is refreshing

    public ObservableCollection<Venue> Venues { get; } = [];

    internal event Action<Venue, HashSet<Event>>? EventsScraped;
    internal event Action<string, string>? VenueRenamed;
    internal event Action<string>? VenueDeleted;

    public VenueList(SetJsonFileRepository<Venue> venueRepo, Scraper scraper, INavigation navigation)
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

            // order unscraped (new) venues on top, then by latest refresh
            foreach (var venue in venues.OrderByDescending(v => v.LastRefreshed ?? DateTime.Now))
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

    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(CanRefreshVenue))]
    private async Task RefreshVenueAsync(Venue venue)
    {
        var errors = await RefreshEvents(venue);
        RefreshList();
        await SaveVenues();
        if (errors.Length > 0) await WriteErrorReportAsync(ReportErrors(errors, venue));
    }

    [RelayCommand]
    private async Task RefreshAllVenuesAsync()
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
                await App.CurrentPage.DisplayAlert("Error importing venues", ex.Message, "OK");
                return;
            }

            if (imported?.Count < 1) return;
            Venues.Import(imported!);
            await SaveVenues();
            RefreshList();
        }
    }

    private bool CanRefreshVenue(Venue? venue) => venue is not null && !IsRefreshing(venue);
    private bool IsRefreshing(Venue venue) => refreshingVenues.Contains(venue);

    private void SetVenueRefreshing(Venue venue, bool isRefreshing)
    {
        if (isRefreshing) refreshingVenues.Add(venue);
        else refreshingVenues.Remove(venue);

        /* refreshing venues count against the progress, i.e. all refreshing => 0, none => 1
         * so that the bar progresses as venues finish refreshing */
        RefreshAllVenuesProgress = (Venues.Count - refreshingVenues.Count) / (double)Venues.Count;

        RefreshVenueCommand.NotifyCanExecuteChanged();
    }

    private async Task<Exception[]> RefreshEvents(Venue venue)
    {
        SetVenueRefreshing(venue, true);

        try
        {
            (HashSet<Event> events, Exception[] errors) = await scraper.ScrapeVenueAsync(venue);
            venue.LastRefreshed = DateTime.Now;
            EventsScraped?.Invoke(venue, events); // notify subscribers
            return errors;
        }
        finally
        {
            SetVenueRefreshing(venue, false);
        }
    }

    private Task SaveVenues() => venueRepo.SaveCompleteAsync(Venues.MigrateSelectors().ToHashSet());

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
                    SwingPickaxeDuring(refresh, model.RefreshVenueCommand);

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

            var refreshAllProgress = new ProgressBar().Bind(ProgressBar.ProgressProperty, nameof(RefreshAllVenuesProgress))
                .ToolTip("the progress of refreshing the events of all venues ")
                // hide when none is refreshing
                .BindVisible(nameof(RefreshAllVenuesProgress), converter: Converters.Func<double>(progress => progress < 1d));

            Content = Grd(cols: [Auto, Star, Auto, Auto], rows: [Auto, Star, Auto, Auto], spacing: 5,
                title.ColumnSpan(2), importVenues.Column(2), exportVenues.Column(3),
                list.Row(1).ColumnSpan(4),
                refreshAllProgress.Row(2).ColumnSpan(4),
                addVenue.Row(3), refreshAll.Row(3).Column(2).ColumnSpan(2));
        }

        private void SwingPickaxeDuring(Button btn, ICommand cmd)
        {
            /* square up & round up btn, so that its rotation is not noticable
             * because we cannot access and rotate only its label */
            btn.HeightRequest = btn.WidthRequest = 46;
            btn.CornerRadius = 25;

            CancellationTokenSource? cts = null;

            cmd.CanExecuteChanged += async (_, __) =>
            {
                // The button command's *own* CanExecute result decides whether to start or stop the animation.
                if (cmd.CanExecute(btn.CommandParameter))
                {
                    cts?.Cancel(); // stop animation
                    cts?.Dispose(); // explicitly to avoid memory leaks
                    cts = null; // make it eligible for GC
                    return;
                }

                // can't execute, i.e. should run
                if (cts != null) return; // already running
                cts = new CancellationTokenSource();
                var token = cts.Token; // use separate variable for token because cts may be set null on another thread

                try
                {
                    while (!token.IsCancellationRequested && !cmd.CanExecute(btn.CommandParameter))
                    {
                        await btn.RotateTo(90, 1500, Easing.SinInOut); // retract animation
                        await AnimateHit();
                    }
                }
                finally
                {
                    await AnimateHit(); // final hit resets
                }
            };

            Task<bool> AnimateHit() => btn.RotateTo(0, 300, Easing.SpringOut);
        }
    }

    /// <summary>Wraps the <see cref="View"/> in a stand-alone Page for narrow devices that use AppShell.</summary>
    public partial class Page : ContentPage
    {
        public Page(SetJsonFileRepository<Venue> venueRepo, Scraper scraper, EventRepository eventRepo)
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
