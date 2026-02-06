using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using static FomoCal.Gui.ViewModels.Widgets;

namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    private readonly RememberedStrings recentSearches = new("EventList.RecentSearches", "🔍");

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<EventView> FilteredEvents { get; private set; } = [];
    [ObservableProperty] public partial ObservableCollection<string> RecentSearches { get; private set; }

    private void ApplyFilter()
    {
        CanDeletePastEvents = ShowPastEvents && allEvents!.Any(e => e.IsPast);
        var filtered = ViewSelectedOnly ? selected : ShowPastEvents ? allEvents! : allEvents!.Where(e => !e.IsPast);
        string[] searchTerms;

        if (SearchText.IsSignificant())
        {
            searchTerms = [.. SearchText.Split("|", StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim())];

            filtered = filtered.Where(e => e.Model.Name.ContainsAny(searchTerms)
                || e.Model.SubTitle?.ContainsAny(searchTerms) == true
                || e.Model.Genres?.ContainsAny(searchTerms) == true
                || e.Model.Description?.ContainsAny(searchTerms) == true
                || e.Model.Venue?.ContainsAny(searchTerms) == true
                || e.Model.Stage?.ContainsAny(searchTerms) == true);
        }
        else searchTerms = [];

        suspendSelectionChange = true; // while FilteredEvents are modified, which reset the selection
        FilteredEvents.Clear();

        foreach (var evt in filtered.OrderBy(e => e.Date))
        {
            evt.SetSearchTerms(searchTerms);
            FilteredEvents.Add(evt);
        }

        suspendSelectionChange = false;
    }

    private void SaveSearch()
    {
        if (SearchText.IsNullOrWhiteSpace()) return;

        if (!RecentSearches.Contains(SearchText))
        {
            RecentSearches.Insert(0, SearchText);
            if (RecentSearches.Count > 10) RecentSearches.RemoveAt(RecentSearches.Count - 1);
            recentSearches.Set(RecentSearches);
        }
    }

    [RelayCommand]
    private void DeleteSearch(string query)
    {
        if (RecentSearches.Remove(query))
            recentSearches.Set(RecentSearches);
    }

    partial class View
    {
        private static (SearchBar searchBar, ScrollView recentSearches) BuildSearch(EventList model)
        {
            var searchBar = new SearchBar() { Placeholder = "filter by pipe | separated | terms" }
                .Bind(SearchBar.TextProperty, nameof(SearchText))
                .ToolTip("[Enter] or tap the 🔎 icon to remember the current search for the future");

            searchBar.SearchButtonPressed += (s, e) => model.SaveSearch();

            var recentSearches = new CollectionView
            {
                ItemsSource = model.RecentSearches,
                SelectionMode = SelectionMode.Single,
                ItemTemplate = new DataTemplate(() =>
                    Grd(cols: [Star, Auto], rows: [Auto], spacing: 5,
                        BndLbl().Center(), Btn(Glyphs.Delete, nameof(DeleteSearchCommand), source: model).Column(1)))
            };

            var closeRecentSearches = Btn("⬆️ Close recent searches");

            var scroller = new ScrollView()
            {
                Content = VStack(5, recentSearches, closeRecentSearches),
                IsVisible = false
            };

            EventHandler? limitInitialScrollerSize = null;

            limitInitialScrollerSize = (_, __) =>
            {
                scroller.SizeChanged -= limitInitialScrollerSize!; // unsub initial sizing handler
                var parent = (VisualElement)scroller.Parent;
                LimitScrollerSize(parent); // limit size initially
                parent.SizeChanged += (_, __) => LimitScrollerSize(parent); // and when parent size changes
            };

            // init size on first event that allows correct size measurements
            scroller.SizeChanged += limitInitialScrollerSize;

            // toggle dropdown visibility when searchbar focused
            searchBar.Focused += (_, __) => scroller.IsVisible = true;

            closeRecentSearches.TapGesture(() =>
            {
                scroller.IsVisible = false;
                searchBar.Unfocus();
            });

            searchBar.Unfocused += async (_, __) =>
            {
                await Task.Delay(300); // to enable selecting or deleting recent searches
                if (scroller.IsLoaded) scroller.IsVisible = false;
            };

            // when a recent search is tapped, restore it
            recentSearches.SelectionChanged += (_, __) =>
            {
                if (recentSearches.SelectedItem is string query)
                {
                    model.SearchText = query; // to restore the selected search
                    searchBar.Unfocus(); // to close recent searches
                    recentSearches.SelectedItem = null; // to enable re-selection
                }
            };

            return (searchBar, scroller);

            // limit height to keep everything visible force scroll because the layout it's placed into doesn't do it
            void LimitScrollerSize(VisualElement parent)
                => scroller.MaximumHeightRequest = parent.DesiredSize.Height - searchBar.Height;
        }
    }
}
