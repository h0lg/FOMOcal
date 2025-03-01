using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace FomoCal.Gui.ViewModels;

public partial class EventList : ObservableObject
{
    private readonly EventRepository eventRepo;
    private HashSet<Event>? allEvents;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Event> filteredEvents = [];

    public EventList(EventRepository eventRepo)
    {
        this.eventRepo = eventRepo;

        PropertyChanged += (o, e) =>
        {
            if (e.PropertyName == nameof(SearchText))
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
        IEnumerable<Event> filtered = allEvents!;

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

    [RelayCommand]
    private static async Task OpenUrlAsync(string url)
        => await WebViewPage.OpenUrlAsync(url);

    // used on the MainPage for Desktop
    public partial class View : ContentView
    {
        public View(EventList model)
        {
            BindingContext = model;

            var searchBar = new SearchBar().Placeholder("Filter events")
                .Bind(SearchBar.TextProperty, nameof(SearchText));

            var list = new CollectionView
            {
                ItemsSource = model.FilteredEvents,
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
                            RequiredTextLabel(nameof(Event.Name)).Wrap(),
                            OptionalTextLabel(nameof(Event.SubTitle)).Wrap(),
                            OptionalTextLabel(nameof(Event.Genres)).Wrap() }
                    };

                    var times = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children = {
                            DateLabel(nameof(Event.Date)).Bold(),
                            OptionalTextLabel(nameof(Event.DoorsTime)),
                            OptionalTextLabel(nameof(Event.StartTime)) }
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
                            RequiredTextLabel(nameof(Event.Venue)),
                            OptionalTextLabel(nameof(Event.Stage)),
                            DateLabel(nameof(Event.Scraped)) }
                    };

                    var tickets = new VerticalStackLayout
                    {
                        Spacing = 5,
                        Children = {
                            OptionalTextLabel(nameof(Event.PresalePrice)),
                            OptionalTextLabel(nameof(Event.DoorsPrice)),
                            OpenUrlButton("Tickets", nameof(Event.TicketUrl), model) }
                    };

                    return new Border
                    {
                        Padding = 10,

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
                    };
                })
            };

            Content = new Grid
            {
                ColumnSpacing = 5,
                RowSpacing = 5,
                ColumnDefinitions = Columns.Define(Star),
                RowDefinitions = Rows.Define(Auto, Star),
                Children = {
                    searchBar,
                    list.Row(1)
                }
            };
        }

        private static Label RequiredTextLabel(string property)
            => new Label().Bind(Label.TextProperty, property);

        private static Label DateLabel(string property)
            => new Label().Bind(Label.TextProperty, property, stringFormat: "{0:d}");

        private static Label OptionalTextLabel(string property)
            => new Label().Bind(Label.TextProperty, property).BindIsVisibleToValueOf(property);

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
