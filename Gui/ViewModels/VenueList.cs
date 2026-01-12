using System.Windows.Input;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueList(INavigation navigation, VenueCollection venues) : ObservableObject
{
    private readonly VenueCollection Venues = venues;
    private readonly INavigation navigation = navigation;

    [RelayCommand] private Task AddVenue() => Venues.AddAsync(navigation);
    [RelayCommand] private Task EditVenueAsync(Venue original) => Venues.EditAsync(original, navigation);
    [RelayCommand] private Task DeleteVenueAsync(Venue venue) => Venues.DeleteAsync(venue);

    [RelayCommand]
    private void ExportVenues() => Venues.ShareFile();

    [RelayCommand]
    private async Task ImportVenues()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Pick a venues config",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> {
                { DevicePlatform.Android, ["application/json"] },
                { DevicePlatform.WinUI, [".json"] } })
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
                await App.CurrentPage.DisplayAlertAsync("Error importing venues", ex.Message, "OK");
                return;
            }

            if (imported?.Count < 1) return;
            await Venues.Import(imported!);
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync() => await navigation.PushAsync(new Settings.Page(new Settings()));

    public partial class View : ContentView
    {
        public View(VenueList model)
        {
            BindingContext = model;

            var list = new CollectionView()
                .Bind(ItemsView.ItemsSourceProperty, nameof(VenueCollection.Observable), source: model.Venues)
                .ItemTemplate(new DataTemplate(() =>
                {
                    var name = BndLbl(nameof(Venue.Name)).FontSize(16).Wrap();
                    var location = BndLbl(nameof(Venue.Location)).StyleClass(Styles.Label.VenueRowDetail);

                    var lastEventCount = BndLbl(nameof(Venue.LastEventCount))
                        .StyleClass(Styles.Label.VenueRowDetail)
                        .BindIsVisibleToHasValueOf<Label, int>(nameof(Venue.LastEventCount));

                    var lastRefreshed = BndLbl(nameof(Venue.LastRefreshed), stringFormat: $"last {Glyphs.Scrape} {{0:d MMM H:mm}}")
                        .StyleClass(Styles.Label.VenueRowDetail)
                        .BindIsVisibleToHasValueOf<Label, DateTime>(nameof(Venue.LastRefreshed));

                    var refresh = Btn(Glyphs.Scrape, nameof(VenueCollection.RefreshVenueCommand), source: model.Venues);
                    SwingPickaxeDuring(refresh, model.Venues.RefreshVenueCommand);

                    var border = new Border
                    {
                        Padding = 10,
                        Content = Grd(cols: [Star, Auto], rows: [Auto, Auto, Auto], spacing: 5,
                            name.ColumnSpan(2),
                            location.Row(1),
                            refresh.Row(1).Column(1).RowSpan(2).Bottom(),
                            HStack(5, lastEventCount, lastRefreshed).View.Row(2).End())
                    }.BindTapGesture(nameof(EditVenueCommand), commandSource: model, parameterPath: ".");

                    if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
                    {
                        MenuFlyout menu = [
                            new MenuFlyoutItem() { Text = Glyphs.Edit + " Edit" }.BindCommand(nameof(EditVenueCommand), source: model),
                            new MenuFlyoutItem() { Text = Glyphs.Delete + " Delete" }.BindCommand(nameof(DeleteVenueCommand), source: model)];

                        FlyoutBase.SetContextFlyout(border, menu);
                        return border;
                    }
                    else return new SwipeView()
                    {
                        LeftItems = [new SwipeItem() { Text = Glyphs.Edit + " Edit" }.BindCommand(nameof(EditVenueCommand), source: model)],
                        Content = border,
                        RightItems = [new SwipeItem() { Text = Glyphs.Delete + " Delete" }.BindCommand(nameof(DeleteVenueCommand), source: model)]
                    };
                }));

            var importVenues = Btn("📥", nameof(ImportVenuesCommand)).ToolTip("import venues");
            var exportVenues = Btn(Glyphs.Export, nameof(ExportVenuesCommand)).ToolTip("export venues");
            var addVenue = Btn(Glyphs.Add, nameof(AddVenueCommand)).ToolTip("add a venue");

            var refreshAll = Btn(Glyphs.Scrape + " dig all gigs",
                nameof(VenueCollection.RefreshAllVenuesCommand), source: model.Venues)
                .ToolTip("refresh events from all venues");

            var refreshAllProgress = new ProgressBar()
                .Bind(ProgressBar.ProgressProperty, nameof(VenueCollection.RefreshAllVenuesProgress), source: model.Venues)
                .ToolTip("the progress of refreshing the events of all venues ")
                // hide when none is refreshing
                .BindVisible(nameof(VenueCollection.RefreshAllVenuesProgress), source: model.Venues,
                    converter: Converters.Func<double>(progress => progress < 1d));

            if (Shell.Current == null) // desktop layout with Venue and Event list side by side
            {
                // title display and settings access are take care of by tabs in the Shell
                var title = Lbl(Glyphs.Venue + "Venues").StyleClass(Styles.Label.Headline).CenterVertical();
                var openSettings = Btn(Glyphs.Settings, nameof(OpenSettingsCommand)).ToolTip("open Settings");

                Content = Grd(cols: [Auto, Auto, Star, Auto, Auto], rows: [Auto, Star, Auto, Auto], spacing: 5,
                    title.ColumnSpan(3), importVenues.Column(3), exportVenues.Column(4),
                    list.Row(1).ColumnSpan(5),
                    refreshAllProgress.Row(2).ColumnSpan(5),
                    openSettings.Row(3), addVenue.Row(3).Column(1), refreshAll.Row(3).Column(3).ColumnSpan(2));
            }
            else // shell layout displaying lists separately
                Content = Grd(cols: [Auto, Auto, Auto, Star, Auto], rows: [Star, Auto, Auto], spacing: 5,
                    list.ColumnSpan(5),
                    refreshAllProgress.Row(1).ColumnSpan(5),
                    addVenue.Row(2), importVenues.Row(2).Column(1), exportVenues.Row(2).Column(2),
                    refreshAll.Row(2).Column(4));
        }

        private static void SwingPickaxeDuring(Button btn, ICommand cmd)
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
                        await btn.RotateToAsync(90, 1500, Easing.SinInOut); // retract animation
                        await AnimateHit();
                    }
                }
                finally
                {
                    await AnimateHit(); // final hit resets
                }
            };

            Task<bool> AnimateHit() => btn.RotateToAsync(0, 300, Easing.SpringOut);
        }
    }

    /// <summary>Wraps the <see cref="View"/> in a stand-alone Page for narrow devices that use AppShell.</summary>
    public partial class Page : ContentPage
    {
        public Page(VenueCollection venues, EventRepository eventRepo)
        {
            Title = "Venues";
            VenueList venueList = new(Navigation, venues);
            venues.EventsScraped += async (venue, events) => await eventRepo.AddOrUpdateAsync(venue, events);
            venues.Renamed += async (oldName, newName) => await eventRepo.RenameVenueAsync(oldName, newName);
            venues.Deleted += async (venueName) => await eventRepo.DeleteVenueAsync(venueName);
            Content = new View(venueList);
            var loaded = false;

            NavigatedTo += async (o, e) =>
            {
                if (loaded) return;
                loaded = true;
                await venues.LoadAsync();
            };
        }
    }
}
