using System.Windows.Input;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class VenueList(INavigation navigation, VenueCollection venues, EventRepository eventRepo) : ObservableObject
{
    private readonly VenueCollection Venues = venues;
    private readonly INavigation navigation = navigation;

    [RelayCommand] private Task AddVenue() => Venues.AddAsync(navigation);
    [RelayCommand] private Task EditVenueAsync(Venue original) => Venues.EditAsync(original, navigation);
    [RelayCommand] private Task DeleteVenueAsync(Venue venue) => Venues.DeleteAsync(venue);

    private async Task ShowMenu()
    {
        const string import = "📥 Import",
            share = Glyphs.Export + " Share",
            openSettings = $"Open {Glyphs.Settings} Settings";

        List<string> options = [import, share];
        if (Shell.Current == null) options.Add(openSettings); // add open settings menu entry outside of shell
        var choice = await App.CurrentPage.DisplayActionSheetAsync(Glyphs.Venue + "Venues", null, null, [.. options]);

        if (choice == import) await Venues.Import(navigation, eventRepo);
        else if (choice == share) Venues.ShareFile();
        else if (choice == openSettings) await Settings.Page.GoHere(navigation);
    }

    public partial class View : ContentView
    {
        public View(VenueList model)
        {
            BindingContext = model;

            var list = new CollectionView()
            {
                // adds some space so that floating action buttons don't overlay the last item
                Footer = Lbl("The\nEnd").TextColor(Colors.Transparent).StyleClass(Styles.Label.Headline)
            }
                .RowSpan(2) // span into next row so that buttons following it become floating action buttons
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
                        StrokeThickness = 0,
                        Content = Grd(cols: [Star, Auto], rows: [Auto, Auto, Auto], spacing: 5,
                            name.ColumnSpan(2),
                            location.Row(1),
                            refresh.Row(1).Column(1).RowSpan(2).Bottom(),
                            HStack(5, lastEventCount, lastRefreshed).Row(2).End())
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

            var addVenue = Btn(Glyphs.Add, nameof(AddVenueCommand)).ToolTip("add a venue").Margin(3);

            var refreshAll = Btn(Glyphs.Scrape + " dig all gigs",
                nameof(VenueCollection.RefreshAllVenuesCommand), source: model.Venues)
                .ToolTip("refresh events from all venues").Margin(3);

            var menuTrigger = MenuTrigger(async () => await model.ShowMenu());

            var refreshAllProgress = new ProgressBar()
                .Bind(ProgressBar.ProgressProperty, nameof(VenueCollection.RefreshAllVenuesProgress), source: model.Venues)
                .ToolTip("the progress of refreshing the events of all venues ")
                // hide when none is refreshing
                .BindVisible(nameof(VenueCollection.RefreshAllVenuesProgress), source: model.Venues,
                    converter: Converters.Predicate<double>(progress => progress < 1d));

            if (Shell.Current == null) // desktop layout with Venue and Event list side by side
            {
                // title display and settings access are take care of by tabs in the Shell
                var title = Lbl(Glyphs.Venue + "Venues").StyleClass(Styles.Label.Headline).CenterVertical();

                Content = Grd(cols: [Star, Auto], rows: [44, Star, Auto, Auto], spacing: 0,
                    title, menuTrigger.Column(2).End(),
                    list.Row(1).ColumnSpan(2),
                    addVenue.Row(2).Start(), refreshAll.Row(2).Column(1),
                    refreshAllProgress.Row(3).ColumnSpan(2));
            }
            else // shell layout displaying lists separately
                Content = Grd(cols: [Auto, Star, Auto], rows: [Star, Auto, Auto], spacing: 0,
                    list.ColumnSpan(3),
                    addVenue.Row(1).Start(), refreshAll.Row(1).Column(1).CenterHorizontal(), menuTrigger.Row(1).Column(2),
                    refreshAllProgress.Row(2).ColumnSpan(3));
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
            VenueList venueList = new(Navigation, venues, eventRepo);
            venues.EventsScraped += async (venue, events) => await eventRepo.AddOrUpdateAsync(venue, events);
            venues.Renamed += async (oldName, newName) => await eventRepo.RenameVenueAsync(oldName, newName);
            venues.Deleted += async (venueName) => await eventRepo.DeleteVenueAsync(venueName);
            Content = new View(venueList);

            NavigatedTo += LoadOnce;

            async void LoadOnce(object? sender, NavigatedToEventArgs e)
            {
                NavigatedTo -= LoadOnce;
                await venues.LoadAsync();
            }
        }
    }
}
