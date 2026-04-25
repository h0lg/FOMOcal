using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class EventList : ObservableObject
{
    private readonly VenueCollection venues;
    private readonly EventRepository eventRepo;
    private readonly INavigation navigation;
    private HashSet<EventView>? allEvents;
    private bool hasPastEvents;

    [ObservableProperty] public partial bool ShowPastEvents { get; set; }

    public EventList(EventRepository eventRepo, VenueCollection venues, INavigation navigation)
    {
        this.venues = venues;
        this.eventRepo = eventRepo;
        this.navigation = navigation;
        RecentSearches = new(MigrateRecentSearches(recentSearches.Get()));

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(ShowPastEvents)
                || e.PropertyName == nameof(SearchText))
            {
                ApplyFilter(); // because source collection or search text changed
                NotifySelectionChanged(); // because FilteredEvents changed
            }
        };

        // re-render filtered items to apply correct styles on theme change
        Application.Current!.RequestedThemeChanged += (o, e) =>
        {
            foreach (var evt in FilteredEvents)
                evt.RefreshTextRendering();
        };
    }

    // Called from the MainPage on VenueList.EventsScraped
    internal void RefreshWith(Venue venue, HashSet<Event> newEvents)
    {
        if (newEvents.Count == 0) return;

        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // merge new events into existing
            allEvents!.UpdateWith(venue, [.. newEvents.Select(e => new EventView(e))]);
            await OnEventsUpdated();
        });
    }

    internal void RenameVenue(string oldName, string newName)
    {
        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Venue name changes propagate through the EventView, so allEvents needs no update
            HashSet<Event> events = GetEvents();
            events.RenameVenue(oldName, newName);
            await OnEventsUpdated(events);
        });
    }

    internal void DeleteForVenue(string venue)
    {
        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            allEvents!.RemoveOfVenue(venue);
            await OnEventsUpdated();
        });
    }

    internal async Task LoadEvents()
    {
        try
        {
            var events = await eventRepo.LoadAllAsync();

            // auto-clean up events older than 3 months
            var oldest = DateTime.Today.AddMonths(-3);
            var removed = events.RemoveWhere(evt => evt.Date < oldest);
            if (removed > 0) await eventRepo.SaveCompleteAsync(events);

            // transform events into view model collection once
            allEvents = [.. events.Select(e => new EventView(e))];
        }
        catch (Exception ex)
        {
            allEvents = [];
            await ErrorReport.WriteAsyncAndShare(ex.ToString(), "loading events");
        }

        ApplyFilter(); // to fill FilteredEvents initially
    }

    /// <summary>Call after <see cref="allEvents"/> changed (by adding to or removing from it)
    /// to persist its <see cref="EventView.Model"/>s
    /// - or the given <paramref name="events"/> after updating them in place.
    /// In either way, the complete event collection is expected.</summary>
    private Task OnEventsUpdated(HashSet<Event>? events = null)
    {
        OnPropertyChanged(nameof(EventCounters)); // uses allEvents count
        ApplyFilter(); // re-apply filter after events updated to refresh CollectionView
        NotifySelectionChanged(); // because FilteredEvents changed
        return eventRepo.SaveCompleteAsync(events ?? GetEvents());
    }

    private HashSet<Event> GetEvents() => [.. allEvents!.GetEvents()];

    [RelayCommand]
    private static async Task OpenUrlAsync(string url)
        => await WebViewPage.OpenUrlAsync(url);

    private async Task CleanUpPastEventsAsync()
    {
        allEvents!.RemoveWhere(e => e.IsPast);
        await OnEventsUpdated();
    }

    private async Task ShowMenu()
    {
        const string showPast = "👁 Show 🕞 past",
            deleteSelected = Glyphs.Delete + " Delete all ☑ selected",
            hidePast = "🙈 Hide 🕞 past",
            deletePast = Glyphs.Delete + " Delete 🕞 past";

        string inView = ViewSelectedOnly ? SelectedEventCounters : EventCounters,
            selectAll = Glyphs.Select + "Select all " + inView,
            deselectAll = Glyphs.Deselect + "Deselect all " + inView;

        List<string> options = [];

        var overlap = FilteredEvents.Intersect(selected).ToArray();
        if (overlap.Length < FilteredEvents.Count) options.Add(selectAll); // allow select all if not all are selected
        if (0 < overlap.Length) options.Add(deselectAll); // allow deselect all if any are selected
        if (HasSelection) options.Add(deleteSelected);

        if (hasPastEvents)
        {
            options.Add(ShowPastEvents ? hidePast : showPast);
            options.Add(deletePast);
        }

        var choice = await App.CurrentPage.DisplayActionSheetAsync("Gigs", null, null, [.. options]);

        if (choice == selectAll) SelectFilteredEvents();
        else if (choice == deselectAll) DeselectFilteredEvents();
        else if (choice == deleteSelected) await DeleteSelectedEventsAsync();
        else if (choice == showPast) ShowPastEvents = true;
        else if (choice == hidePast) ShowPastEvents = false;
        else if (choice == deletePast) await CleanUpPastEventsAsync();
    }

    [RelayCommand] private Task EditVenueAsync(EventView view) => venues.EditAsync(view.Model.Venue, navigation);
    [RelayCommand] private Task RefreshVenueAsync(EventView view) => venues.RefreshByNameAsync(view.Model.Venue);

    [RelayCommand]
    private async Task DeleteEventAsync(EventView view)
    {
        allEvents!.Remove(view);
        selected.Remove(view);
        if (selected.Count == 0) ViewSelectedOnly = false;
        NotifySelectionChanged();
        await OnEventsUpdated();
    }

    // used on the MainPage for Desktop
    public partial class View : ContentView
    {
        private static readonly TextChunkConverter textChunkConverter =
            new(linkStyle: Styles.Span.Link, highlitStyle: Styles.Span.Highlit, normalStyle: Styles.Span.Normal);

        public View(EventList model)
        {
            BindingContext = model;
            bool isDesktop = DeviceInfo.Idiom == DeviceIdiom.Desktop;
            (SearchBar searchBar, ScrollView recentSearches) = BuildSearch(model);

            bool UseVerticalEventLayout() => Width < 800; // whether to stack image on top of event details
            var useVerticalEventLayout = UseVerticalEventLayout(); // caches the last result

            DataTemplate eventTemplate = new(() =>
            {
                var image = new Image()
                    .Bind(Image.SourceProperty, nameof(EventView.ImageUrl),
                        convert: static (string? url) => url.IsNullOrWhiteSpace() ? null
                            : new UriImageSource { Uri = new Uri(url!), CacheValidity = TimeSpan.FromDays(30) })
                    .BindVisibleToSignificanceOf(nameof(EventView.ImageUrl));

                var header = VStack(5,
                    BndFmtLbl(nameof(EventView.Name), converter: textChunkConverter).Bold().Wrap().FontSize(16),
                    OptionalFormattedLabel(nameof(EventView.SubTitle)).Bold().Wrap(),
                    OptionalFormattedLabel(nameof(EventView.Genres)).Wrap());

                var times = VStack(5,
                    BndLbl(nameof(EventView.Date), stringFormat: Glyphs.Date + "{0:ddd d MMM yy}").Bold(),
                    OptionalTextLabel(nameof(EventView.DoorsTime), Glyphs.Doors + "{0}"),
                    OptionalTextLabel(nameof(EventView.StartTime), Glyphs.Start + "{0}"));

                var details = Grd([Star, Auto], [Auto, Auto], spacing: 5,
                    OptionalFormattedLabel(nameof(EventView.Description)).Wrap().ColumnSpan(2),
                    BndLbl(nameof(EventView.Scraped), stringFormat: Glyphs.Scrape + " {0:d MMM}")
                        .StyleClass(Styles.Label.Demoted).CenterVertical().Row(1),
                    OpenUrlButton(Glyphs.EventPage + "more " + Glyphs.Link, nameof(EventView.Url), model).Row(1).Column(1),
                    OpenUrlButton(Glyphs.Scrape + " from " + Glyphs.Link, nameof(EventView.ScrapedFrom), model).Row(1).Column(1));

                var location = HWrap(5,
                    BndFmtLbl(nameof(EventView.Venue), converter: textChunkConverter),
                    OptionalFormattedLabel(nameof(EventView.Stage))).View;

                var tickets = VStack(5,
                    OptionalTextLabel(nameof(EventView.PresalePrice), Glyphs.PresalePrice + "{0}"),
                    OptionalTextLabel(nameof(EventView.DoorsPrice), Glyphs.DoorPrice + "{0}"),
                    OpenUrlButton(Glyphs.Tickets + "Tickets " + Glyphs.Link, nameof(EventView.TicketUrl), model));

                Grid eventLayout = useVerticalEventLayout
                    ? Grd(cols: [Star, Auto], rows: [Auto, Auto, Auto, Auto], spacing: 5,
                        image.Bind(HeightRequestProperty, nameof(EventView.ImageHeight)).ColumnSpan(2),
                        header.Row(1), times.Row(1).Column(1),
                        details.Row(2).ColumnSpan(2),
                        location.Bottom().Row(3), tickets.Row(3).Column(1))
                    : Grd(cols: [200, Star, Auto], rows: [Auto, Auto, Auto], spacing: 5,
                        image.RowSpan(3),
                        header.Column(1), times.Column(2),
                        details.Row(1).Column(1).ColumnSpan(2),
                        location.Bottom().Row(2).Column(1), tickets.Row(2).Column(2));

                var border = new Border
                {
                    StyleClass = ["list-event"],
                    Content = eventLayout
                }
                .Bind(OpacityProperty, nameof(EventView.IsPast),
                    convert: static (bool isPast) => isPast ? 0.5 : 1.0);

                border
                    .Bind(Selection.IsSelectedProperty, nameof(EventView.IsSelected))
                    .TapGesture(() => ToggleSelected((EventView)border.BindingContext, model));

                if (isDesktop)
                {
                    MenuFlyout menu = [
                        new MenuFlyoutItem() { Text = $"{Glyphs.Scrape} Refresh events from {Glyphs.Venue} venue" }.BindCommand(nameof(RefreshVenueCommand), source: model),
                        new MenuFlyoutItem() { Text = $"{Glyphs.Edit} Edit {Glyphs.Venue} venue" }.BindCommand(nameof(EditVenueCommand), source: model),
                        new MenuFlyoutItem() { Text = Glyphs.Delete + " Delete" }.BindCommand(nameof(DeleteEventCommand), source: model)];

                    FlyoutBase.SetContextFlyout(border, menu);
                    return border;
                }
                else
                {
                    // shown on swipe right - works around SwipeView not supporting vertical layout
                    FlexLayout leftMenu = new()
                    {
                        Direction = FlexDirection.Column,
                        JustifyContent = FlexJustify.SpaceEvenly,
                        AlignItems = FlexAlignItems.Center,
                        Children = {
                            Btn($"{Glyphs.Scrape} Refresh events from {Glyphs.Venue} venue", nameof(RefreshVenueCommand), source: model).Wrap(),
                            Btn($"{Glyphs.Edit} Edit {Glyphs.Venue} venue", nameof(EditVenueCommand), source: model).Wrap()
                        }
                    };

                    return new SwipeView()
                    {
                        StyleClass = ["list-event"],
                        LeftItems = [new SwipeItemView() { Content = leftMenu }],
                        Content = border,
                        RightItems = [new SwipeItem() { Text = Glyphs.Delete + " Delete" }.BindCommand(nameof(DeleteEventCommand), source: model)]
                    };
                }
            });

            var list = new CollectionView
            {
                ItemsSource = model.FilteredEvents,
                ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset, // Prevents flickering
                ItemTemplate = eventTemplate
            };

            SizeChanged += (o, e) =>
            {
                // skip expensive template reset if it wouldn't change anything
                if (useVerticalEventLayout == UseVerticalEventLayout()) return;
                useVerticalEventLayout = !useVerticalEventLayout;

                // re-apply ItemTemplate to update layout of existing items
                list.ItemTemplate = null;
                list.ItemTemplate = eventTemplate;
            };

            var header = Grd(cols: [Auto, Star, Auto], rows: [Auto], spacing: 5,
                Lbl("Gigs").StyleClass(Styles.Label.Headline).CenterVertical().IsVisible(isDesktop),
                searchBar.Column(1),
                MenuTrigger(async () => await model.ShowMenu()).Column(2));

            Content = Grd(cols: [Star], rows: [Auto, Auto, Star, Auto], spacing: 0,
                header, recentSearches.Row(1), list.Row(2).RowSpan(2), SelectionMenu().Row(3));
        }

        private static Label OptionalTextLabel(string property, string? stringFormat = null)
            => BndLbl(property, stringFormat: stringFormat).Wrap().BindVisibleToSignificanceOf(property);

        private static Label OptionalFormattedLabel(string property)
            => BndFmtLbl(property, converter: textChunkConverter).BindVisibleToNotNullOf(property);

        private static Button OpenUrlButton(string text, string urlProperty, object source)
            => Btn(text, nameof(OpenUrlCommand), source: source, parameterPath: urlProperty)
                .BindVisibleToSignificanceOf(urlProperty);
    }

    // used in the AppShell for non-Desktop devices
    public partial class Page : ContentPage
    {
        public Page(EventRepository eventRepo, VenueCollection venues)
        {
            Title = "Events";
            EventList eventList = new(eventRepo, venues, Navigation);
            Content = new View(eventList);
            Shell.SetNavBarIsVisible(this, false); // to save space

            // refresh events when navigated to, in case they were refreshed
            NavigatedTo += async (_, __) => await eventList.LoadEvents();
        }
    }
}
