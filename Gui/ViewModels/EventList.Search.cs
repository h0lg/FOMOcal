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
    [ObservableProperty] public partial ObservableCollection<Event> FilteredEvents { get; private set; } = [];
    [ObservableProperty] public partial ObservableCollection<string> RecentSearches { get; private set; }

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
        private static (SearchBar searchBar, CollectionView recentSearches) BuildSearch(EventList model)
        {
            var searchBar = new SearchBar() { Placeholder = "filter by pipe | separated | terms" }
                .Bind(SearchBar.TextProperty, nameof(SearchText))
                .ToolTip("[Enter] or tap the 🔎 icon to remember the current search for the future");

            searchBar.SearchButtonPressed += (s, e) => model.SaveSearch();

            var recentSearches = new CollectionView
            {
                ItemsSource = model.RecentSearches,
                SelectionMode = SelectionMode.Single,
                IsVisible = false,
                ItemTemplate = new DataTemplate(() =>
                    Grd(cols: [Star, Auto], rows: [Auto], spacing: 5,
                        BndLbl().Center(), Btn("🗑", nameof(DeleteSearchCommand), source: model).Column(1)))
            };

            // toggle dropdown visibility when searchbar focused
            searchBar.Focused += (_, __) => recentSearches.IsVisible = true;

            searchBar.Unfocused += async (_, __) =>
            {
                await Task.Delay(300); // to enable selecting or deleting recent searches
                recentSearches.IsVisible = false;
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

            return (searchBar, recentSearches);
        }
    }
}
