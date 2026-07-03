using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

partial class VenueList
{
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<Venue> FilteredVenues { get; private set; } = [];

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<Venue> filtered = Venues.Observable;
        string[] searchTerms;

        if (SearchText.IsSignificant())
        {
            searchTerms = [.. SearchText.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim())];

            filtered = filtered.Where(v => v.Name.ContainsAny(searchTerms)
                || v.Location?.ContainsAny(searchTerms) == true);
        }
        else searchTerms = [];

        FilteredVenues.Clear();

        foreach (var venue in filtered)
            FilteredVenues.Add(venue);
    }

    partial class View
    {
        private static SearchBar BuildSearch()
        {
            /* Build different placeholders for different platforms;
             * On the Desktop, without Shell Tabs, the placeholder serves as a title (venues),
             * establishing the icon reused in the event list.
             * The details about comma-separating search terms would be cut off in the narrower
             * Venue list Desktop layout - so they go into the tooltip.
             * In the Shell, without a pointer that can hover, the tooltip is less accessible.
             * So that detail can go into placeholder, which doesn't have to repeat the entity or icon. */
            var forDesktop = Shell.Current == null;
            var placeholder = forDesktop ? $"filter {Glyphs.Venue}venues" : "filter by comma,separated,terms";
            SearchBar searchBar = new() { Placeholder = placeholder };
            if (forDesktop) searchBar.ToolTip("comma,separate,multiple search terms");
            return searchBar.Bind(SearchBar.TextProperty, nameof(SearchText));
        }
    }
}
