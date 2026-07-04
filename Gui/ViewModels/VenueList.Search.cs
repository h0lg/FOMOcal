using System.Collections.ObjectModel;
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
}
