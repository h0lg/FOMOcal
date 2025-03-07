﻿using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

public partial class EventList : ObservableObject
{
    private EventRepository eventRepo;
    private HashSet<Event>? allEvents;

    [ObservableProperty] private bool showPastEvents;
    [ObservableProperty] private bool canDeletePastEvents;
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Event> filteredEvents = [];
    [ObservableProperty] private IList<object> selectedEvents = [];
    [ObservableProperty] private bool hasSelection;

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
    internal void RefreshWith(List<Event> newEvents)
    {
        if (newEvents.Count == 0) return;

        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            allEvents!.UpdateWith(newEvents);
            ApplyFilter(); // re-apply filter
            await eventRepo.SaveCompleteAsync(allEvents!);
        });
    }

    internal void RenameVenue(string oldName, string newName)
    {
        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            allEvents!.RenameVenue(oldName, newName);
            ApplyFilter(); // re-apply filter
            await eventRepo.SaveCompleteAsync(allEvents!);
        });
    }

    internal void DeleteForVenue(string venue)
    {
        // Ensure UI updates happen on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            allEvents!.RemoveOfVenue(venue);
            ApplyFilter(); // re-apply filter
            await eventRepo.SaveCompleteAsync(allEvents!);
        });
    }

    internal async Task LoadEvents()
    {
        allEvents = await eventRepo.LoadAllAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        CanDeletePastEvents = ShowPastEvents && allEvents!.Any(e => e.IsPast);
        var filtered = ShowPastEvents ? allEvents! : allEvents!.Where(e => !e.IsPast);

        if (SearchText.HasSignificantValue())
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

    [RelayCommand]
    private async Task OpenUrlAsync(string url)
        => await WebViewPage.OpenUrlAsync(url);

    [RelayCommand]
    private async Task CleanUpPastEvents()
    {
        allEvents!.RemoveWhere(e => e.IsPast);
        ApplyFilter();
        await eventRepo.SaveCompleteAsync(allEvents);
    }

    [RelayCommand]
    private void SelectAllEvents()
    {
        if (SelectedEvents.Count == FilteredEvents.Count)
            SelectedEvents.Clear();
        else
        {
            SelectedEvents.Clear();

            foreach (var evt in FilteredEvents)
                SelectedEvents.Add(evt);
        }

        OnPropertyChanged(nameof(SelectedEvents));
    }

    [RelayCommand]
    private async Task ExportToIcsAsync()
    {
        if (SelectedEvents.Any()) await SelectedEvents.Cast<Event>().ExportToIcs();
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

            var pastEvents = new HorizontalStackLayout
            {
                Spacing = 5,
                Children = {
                    Button("🗑", nameof(CleanUpPastEventsCommand))
                        .Bind(IsVisibleProperty, nameof(CanDeletePastEvents)),

                    new Label().Text("🕰 Past gigs").CenterVertical().Bold(),
                    new CheckBox().Bind(CheckBox.IsCheckedProperty, nameof(ShowPastEvents)) }
            };

            var searchBar = new SearchBar().Placeholder("Filter gigs by pipe | separated | terms")
                .Bind(SearchBar.TextProperty, nameof(SearchText));

            var commands = new HorizontalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    Button("👆 de/select all", nameof(SelectAllEventsCommand)),
                    new Label().Text("🥡 export selected as").CenterVertical().Bind(IsVisibleProperty, nameof(HasSelection)),
                    ExportButton("📆 iCal", nameof(ExportToIcsCommand)),
                    ExportButton("▦ CSV", nameof(ExportToCsvCommand))
                }
            };

            var list = new CollectionView
            {
                ItemsSource = model.FilteredEvents,
                SelectionMode = SelectionMode.Multiple,
                ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepScrollOffset, // Prevents flickering
                ItemTemplate = new DataTemplate(() =>
                {
                    var image = new Image { MaximumHeightRequest = 200 }
                        .Bind(Image.SourceProperty, nameof(Event.ImageUrl),
                            convert: static (string? url) => url.IsNullOrWhiteSpace() ? null
                                : new UriImageSource { Uri = new Uri(url!), CacheValidity = TimeSpan.FromDays(30) })
                        .BindIsVisibleToValueOf(nameof(Event.ImageUrl));

                    var header = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children = {
                            RequiredTextLabel(nameof(Event.Name)).Wrap().Bold().FontSize(16),
                            OptionalTextLabel(nameof(Event.SubTitle)).Bold().Wrap(),
                            OptionalTextLabel(nameof(Event.Genres), "🎷 {0}").Wrap() }
                    };

                    var times = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children = {
                            FormattedLabel(nameof(Event.Date), "{0:d}").Bold(),
                            OptionalTextLabel(nameof(Event.DoorsTime), "🚪 {0}"),
                            OptionalTextLabel(nameof(Event.StartTime), "🎼 {0}") }
                    };

                    var details = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children =
                        {
                            OptionalTextLabel(nameof(Event.Description)).Wrap(),
                            OpenUrlButton("more", nameof(Event.Url), model).End()
                        }
                    };

                    var location = new HorizontalStackLayout
                    {
                        Spacing = 5,
                        Children = {
                            RequiredTextLabel(nameof(Event.Venue), "📍 {0}"),
                            OptionalTextLabel(nameof(Event.Stage), "🎤 {0}"),
                            FormattedLabel(nameof(Event.Scraped), "⛏ {0:g}") }
                    };

                    var tickets = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children = {
                            OptionalTextLabel(nameof(Event.PresalePrice), "💳 {0}"),
                            OptionalTextLabel(nameof(Event.DoorsPrice), "💵 {0}"),
                            OpenUrlButton("🎫 Tickets", nameof(Event.TicketUrl), model) }
                    };

                    return new Border
                    {
                        Padding = 10,

                        //TODO do in CSS or extension if needed
                        //StrokeShape = new RoundRectangle { CornerRadius = 8 },
                        Content = new Grid
                        {
                            RowSpacing = 5,
                            ColumnSpacing = 5,
                            ColumnDefinitions = Columns.Define(Auto, Star, Auto),
                            RowDefinitions = Rows.Define(Auto, Auto, Auto),
                            Children = {
                                image.RowSpan(3),
                                header.Column(1), times.Column(2),
                                details.Row(1).Column(1).ColumnSpan(2),
                                location.Row(2).Column(1), tickets.Row(2).Column(2)
                            }
                        }
                    }
                    .Bind(OpacityProperty, nameof(Event.IsPast),
                        convert: static (bool isPast) => isPast ? 0.5 : 1.0);
                })
            }
                .Bind(SelectableItemsView.SelectedItemsProperty, nameof(SelectedEvents))
                .StyleSelected(typeof(Border), BackgroundColorProperty, Colors.Indigo);

            list.SelectionChanged += (o, e) => model.HasSelection = e.CurrentSelection.Count > 0;

            Content = new Grid
            {
                ColumnSpacing = 5,
                RowSpacing = 5,
                ColumnDefinitions = Columns.Define(Star),
                RowDefinitions = Rows.Define(Auto, Star),
                Children = {
                    new FlexLayout{
                        Wrap = FlexWrap.Wrap,
                        Children = { pastEvents, searchBar.Grow(1), commands }
                    },
                    list.Row(1)
                }
            };
        }

        private static Label RequiredTextLabel(string property, string? stringFormat = null)
            => new Label().Bind(Label.TextProperty, property, stringFormat: stringFormat);

        private static Label FormattedLabel(string property, string stringFormat)
            => new Label().Bind(Label.TextProperty, property, stringFormat: stringFormat);

        private static Label OptionalTextLabel(string property, string? stringFormat = null)
            => new Label().Bind(Label.TextProperty, property, stringFormat: stringFormat).BindIsVisibleToValueOf(property);

        private static Button ExportButton(string text, string command)
            => Button(text, command).Bind(IsVisibleProperty, nameof(HasSelection));

        private static Button OpenUrlButton(string text, string urlProperty, object source)
            => new Button().Text(text).BindCommand(nameof(OpenUrlCommand), source: source, parameterPath: urlProperty)
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
