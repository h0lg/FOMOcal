﻿using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class EventList : ObservableObject
{
    private readonly EventRepository eventRepo;
    private HashSet<Event>? allEvents;

    [ObservableProperty] public partial bool ShowPastEvents { get; set; }
    [ObservableProperty] public partial bool CanDeletePastEvents { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<Event> FilteredEvents { get; set; } = [];
    [ObservableProperty] public partial IList<object> SelectedEvents { get; set; } = [];
    public int SelectedEventCount => SelectedEvents.Count;
    public bool HasSelection => SelectedEventCount > 0;

    public EventList(EventRepository eventRepo)
    {
        this.eventRepo = eventRepo;

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(ShowPastEvents)
                || e.PropertyName == nameof(SearchText))
                ApplyFilter();
        };
    }

    // Called from the MainPage on VenueList.EventsScraped
    internal void RefreshWith(HashSet<Event> newEvents)
    {
        if (newEvents.Count == 0) return;

        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            allEvents!.UpdateWith(newEvents);
            await OnEventsUpdated();
        });
    }

    internal void RenameVenue(string oldName, string newName)
    {
        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            allEvents!.RenameVenue(oldName, newName);
            await OnEventsUpdated();
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
            allEvents = await eventRepo.LoadAllAsync();
        }
        catch (Exception ex)
        {
            allEvents = [];
            await ErrorReport.WriteAsyncAndShare(ex.ToString(), "loading events");
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        CanDeletePastEvents = ShowPastEvents && allEvents!.Any(e => e.IsPast);
        var filtered = ShowPastEvents ? allEvents! : allEvents!.Where(e => !e.IsPast);

        if (SearchText.IsSignificant())
        {
            var terms = SearchText.Split("|", StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();

            filtered = filtered.Where(e => e.Name.ContainsAny(terms)
                || e.SubTitle?.ContainsAny(terms) == true
                || e.Genres?.ContainsAny(terms) == true
                || e.Description?.ContainsAny(terms) == true
                || e.Venue?.ContainsAny(terms) == true
                || e.Stage?.ContainsAny(terms) == true);
        }

        FilteredEvents.Clear();

        foreach (var evt in filtered.OrderBy(e => e.Date))
            FilteredEvents.Add(evt);
    }

    /// <summary>Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>.
    /// for read-only properties downstream of <see cref="SelectedEvents"/> -
    /// and for that property itself if <paramref name="forSelectedEvents"/> is true.</summary>
    private void NotifySelectionChanged(bool forSelectedEvents = true)
    {
        if (forSelectedEvents) OnPropertyChanged(nameof(SelectedEvents)); // to notify CollectionView
        OnPropertyChanged(nameof(SelectedEventCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private Task OnEventsUpdated()
    {
        ApplyFilter(); // re-apply filter, updates CollectionView
        return eventRepo.SaveCompleteAsync(allEvents!);
    }

    [RelayCommand]
    private static async Task OpenUrlAsync(string url)
        => await WebViewPage.OpenUrlAsync(url);

    [RelayCommand]
    private async Task CleanUpPastEvents()
    {
        allEvents!.RemoveWhere(e => e.IsPast);
        await OnEventsUpdated();
    }

    [RelayCommand]
    private async Task DeleteSelectedEvents()
    {
        foreach (var evt in SelectedEvents.Cast<Event>())
            allEvents!.Remove(evt);

        SelectedEvents.Clear();
        NotifySelectionChanged();
        await OnEventsUpdated();
    }

    [RelayCommand]
    private void SelectAllEvents()
    {
        if (SelectedEvents.Count == FilteredEvents.Count)
            SelectedEvents.Clear(); // toggle selection, de-selecting all
        else
        {
            SelectedEvents.Clear();

            foreach (var evt in FilteredEvents)
                SelectedEvents.Add(evt);
        }

        NotifySelectionChanged();
    }

    [RelayCommand]
    private async Task ExportToIcsAsync()
    {
        if (SelectedEvents.Any()) await SelectedEvents.Cast<Event>().ExportToIcal();
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (SelectedEvents.Any()) await SelectedEvents.Cast<Event>().ExportToCsv();
    }

    // used on the MainPage for Desktop
    public partial class View : ContentView
    {
        public View(EventList model)
        {
            BindingContext = model;

            var pastEvents = HStack(5,
                Btn("🗑", nameof(CleanUpPastEventsCommand))
                    .BindVisible(nameof(CanDeletePastEvents))
                    .ToolTip("Remove old gig pasta"),

                Lbl("🕞 Past").Bold()
                    .TapGesture(() => model.ShowPastEvents = !model.ShowPastEvents),
                Swtch(nameof(ShowPastEvents)).Wrapper);

            var searchBar = new SearchBar().Placeholder("filter by pipe | separated | terms")
                .Bind(SearchBar.TextProperty, nameof(SearchText));

            var commands = HStack(5,
                Btn("✨ de/select all", nameof(SelectAllEventsCommand)),
                BndLbl(nameof(SelectedEventCount), stringFormat: "{0} selected").BindVisible(nameof(HasSelection)),
                Btn("🗑", nameof(DeleteSelectedEventsCommand)).BindVisible(nameof(HasSelection)),
                Lbl("🥡 export as").BindVisible(nameof(HasSelection)),
                ExportButton("📆 iCal", nameof(ExportToIcsCommand)),
                ExportButton("▦ CSV", nameof(ExportToCsvCommand)));

            bool UseVerticalEventLayout() => Width < 800; // whether to stack image on top of event details
            var useVerticalEventLayout = UseVerticalEventLayout(); // caches the last result

            DataTemplate eventTemplate = new(() =>
            {
                var image = new Image()
                    .Bind(Image.SourceProperty, nameof(Event.ImageUrl),
                        convert: static (string? url) => url.IsNullOrWhiteSpace() ? null
                            : new UriImageSource { Uri = new Uri(url!), CacheValidity = TimeSpan.FromDays(30) })
                    .BindIsVisibleToValueOf(nameof(Event.ImageUrl));

                var header = VStack(5,
                    BndLbl(nameof(Event.Name)).Wrap().Bold().FontSize(16),
                    OptionalTextLabel(nameof(Event.SubTitle)).Bold().Wrap(),
                    OptionalTextLabel(nameof(Event.Genres), "🎶 {0}").Wrap());

                var times = VStack(5,
                    BndLbl(nameof(Event.Date), stringFormat: "📆 {0:ddd d MMM yy}").Bold(),
                    OptionalTextLabel(nameof(Event.DoorsTime), "🚪 {0}"),
                    OptionalTextLabel(nameof(Event.StartTime), "🎼 {0}"));

                var description = new Label().Bind(Label.FormattedTextProperty, nameof(Event.Description),
                    convert: (string? text) => text?.LinkifyUrls(Styles.Span.LinkSpan));

                var details = VStack(5,
                    description.Wrap(),
                    OpenUrlButton("📰 more 📡", nameof(Event.Url), model).End(),
                    OpenUrlButton("⛏ from 📡", nameof(Event.ScrapedFrom), model).End());

                var location = HStack(5,
                    BndLbl(nameof(Event.Venue), stringFormat: "🏟 {0}"),
                    OptionalTextLabel(nameof(Event.Stage), "🏛 {0}"),
                    BndLbl(nameof(Event.Scraped), stringFormat: "⛏ {0:g}")
                        .StyleClass(Styles.Label.Demoted));

                var tickets = VStack(5,
                    OptionalTextLabel(nameof(Event.PresalePrice), "💳 {0}"),
                    OptionalTextLabel(nameof(Event.DoorsPrice), "💵 {0}"),
                    OpenUrlButton("🎫 Tickets 📡", nameof(Event.TicketUrl), model));

                Grid eventLayout = useVerticalEventLayout
                    ? Grd(cols: [Star, Auto], rows: [200, Auto, Auto, Auto], spacing: 5,
                        image.ColumnSpan(2),
                        header.Row(1), times.Row(1).Column(1),
                        details.Row(2).ColumnSpan(2),
                        location.Bottom().Row(3), tickets.Row(3).Column(1))
                    : Grd(cols: [200, Star, Auto], rows: [Auto, Auto, Auto], spacing: 5,
                        image.RowSpan(3),
                        header.Column(1), times.Column(2),
                        details.Row(1).Column(1).ColumnSpan(2),
                        location.Bottom().Row(2).Column(1), tickets.Row(2).Column(2));

                return new Border
                {
                    StyleClass = ["list-event"],
                    Content = eventLayout
                }
                .Bind(OpacityProperty, nameof(Event.IsPast),
                    convert: static (bool isPast) => isPast ? 0.5 : 1.0);
            });

            var list = new CollectionView
            {
                ItemsSource = model.FilteredEvents,
                SelectionMode = SelectionMode.Multiple,
                ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset, // Prevents flickering
                ItemTemplate = eventTemplate
            }
                .Bind(SelectableItemsView.SelectedItemsProperty, nameof(SelectedEvents));

            /*  Notify model properties downstream from SelectedEvents because setting it as
             *  SelectedItemsProperty above doesn't raise PropertyChanged for it on selection.
             *  Don't trigger it for SelectedEvents because we react to the CollectionView here - it doesn't need a call back. */
            list.SelectionChanged += (o, e) => model.NotifySelectionChanged(forSelectedEvents: false);

            SizeChanged += (o, e) =>
            {
                // skip expensive template reset if it wouldn't change anything
                if (useVerticalEventLayout == UseVerticalEventLayout()) return;
                useVerticalEventLayout = !useVerticalEventLayout;

                // re-apply ItemTemplate to update layout of existing items
                list.ItemTemplate = null;
                list.ItemTemplate = eventTemplate;
            };

            Content = Grd(cols: [Star], rows: [Auto, Star], spacing: 5,
                HWrap(new Thickness(0, 0, right: 5, 0), pastEvents,
                    Lbl("Gigs").StyleClass(Styles.Label.Headline), searchBar.Grow(1), commands).View,
                list.Row(1));
        }

        private static Label OptionalTextLabel(string property, string? stringFormat = null)
            => BndLbl(property, stringFormat: stringFormat).BindIsVisibleToValueOf(property);

        private static Button ExportButton(string text, string command)
            => Btn(text, command).BindVisible(nameof(HasSelection));

        private static Button OpenUrlButton(string text, string urlProperty, object source)
            => Btn(text, nameof(OpenUrlCommand), source: source, parameterPath: urlProperty)
                .BindIsVisibleToValueOf(urlProperty);
    }

    // used in the AppShell for non-Desktop devices
    public partial class Page : ContentPage
    {
        public Page(EventList eventList)
        {
            Title = "Events";
            Content = new View(eventList);

            // refresh events when navigated to
            NavigatedTo += async (o, e) => await eventList.LoadEvents();
        }
    }
}
