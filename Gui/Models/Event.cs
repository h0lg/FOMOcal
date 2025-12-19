using System.Reflection;

namespace FomoCal;

public class Event : IHaveAnEvent
{
    public static readonly PropertyInfo[] Fields = typeof(Event).GetProperties();

    public required string Name { get; set; }
    public string? SubTitle { get; set; }
    public string? Genres { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>The day of the event.</summary>
    public required DateTime Date { get; set; }

    public string? DoorsTime { get; set; }
    public string? StartTime { get; set; }

    public required string Venue { get; set; }
    public string? Stage { get; set; }

    /// <summary>The time the event was scraped from the <see cref="Venue"/>.</summary>
    public required DateTime Scraped { get; set; }

    /// <summary>The <see cref="Venue.ProgramUrl"/> the event was scraped from,
    /// set for reference if <see cref="Url"/> is null.</summary>
    public string? ScrapedFrom { get; set; }

    public string? PresalePrice { get; set; }
    public string? DoorsPrice { get; set; }
    public string? TicketUrl { get; set; }

    Event IHaveAnEvent.Event => this;

    public override bool Equals(object? obj) => obj is Event other && Equals(other);

    public bool Equals(Event? other)
    {
        if (other is null) return false;

        // duplicated from GetHashCode to avoid hash collisions
        return Venue == other.Venue
            && (Url ?? Name) == (other.Url ?? other.Name)
            && Date.Date == other.Date.Date;
    }

    public override int GetHashCode() => HashCode.Combine(Venue, Url ?? Name, Date.Date);
    public override string ToString() => $"{Date:d} {Name}"; // for easier debugging
}

public interface IHaveAnEvent
{
    Event Event { get; }
}

internal static class EventExtensions
{
    internal static void UpdateWith<T>(this HashSet<T> existing, Venue venue, HashSet<T> scraped) where T : IHaveAnEvent
    {
        if (scraped.Count == 0) return;

        DateTime min = DateTime.MaxValue, max = DateTime.MinValue;

        foreach (var scrpd in scraped)
        {
            var date = scrpd.Event.Date;
            if (date < min) min = date;
            if (max < date) max = date;
        }

        /* Assume the scraped range is gap-free and remove existing events between min and max date
         * to get rid of outdated events. */
        existing.RemoveWhere(e => (e.Event.Venue == venue.Name && min < e.Event.Date && e.Event.Date < max)
            || scraped.Contains(e)); // de-duplicate remaining using Event.Equals (includes Venue comparison)

        existing.UnionWith(scraped);
    }

    internal static void RenameVenue(this IEnumerable<Event> events, string oldName, string newName)
    {
        foreach (var evt in events)
            if (evt.Venue == oldName)
                evt.Venue = newName;
    }

    internal static void RemoveOfVenue<T>(this HashSet<T> allEvents, string oldName) where T : IHaveAnEvent
        => allEvents.RemoveWhere(e => e.Event.Venue == oldName);
}

public class EventRepository(JsonFileStore store, string fileName) : SetJsonFileRepository<Event>(store, fileName)
{
    internal async Task RenameVenueAsync(string oldName, string newName)
    {
        var set = await LoadAllAsync();
        set.RenameVenue(oldName, newName);
        await SaveCompleteAsync(set);
    }

    internal async Task DeleteVenueAsync(string venue)
    {
        var set = await LoadAllAsync();
        set.RemoveOfVenue(venue);
        await SaveCompleteAsync(set);
    }

    public async Task AddOrUpdateAsync(Venue venue, HashSet<Event> items)
    {
        var set = await LoadAllAsync();
        set.UpdateWith(venue, items);
        await SaveCompleteAsync(set);
    }
}
