using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<Event> FilteredEvents { get; private set; } = [];

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

    partial class View
    {
        private static SearchBar BuildSearch(EventList model)
            => new SearchBar() { Placeholder = "filter by pipe | separated | terms" }
                .Bind(SearchBar.TextProperty, nameof(SearchText));
    }
}
