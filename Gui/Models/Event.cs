using System.Reflection;
using System.Text.Json.Serialization;

namespace FomoCal;

public class Event
{
    public static readonly PropertyInfo[] Fields = [.. typeof(Event).GetProperties().Where(p => p.Name != nameof(IsPast))];

    public required string Name { get; set; }
    public string? SubTitle { get; set; }
    public string? Genres { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>The day of the event.</summary>
    public required DateTime Date { get; set; }

    [JsonIgnore] public bool IsPast => Date < DateTime.Today;
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

    public override bool Equals(object? obj) => obj is Event other && Equals(other);
    public bool Equals(Event? other) => other is not null && GetHashCode() == other.GetHashCode();
    public override int GetHashCode() => HashCode.Combine(Venue, Url ?? Name, Date.Date);
    public override string ToString() => $"{Date:d} {Name}"; // for easier debugging
}

internal static class EventExtensions
{
    internal static void RenameVenue(this IEnumerable<Event> events, string oldName, string newName)
    {
        foreach (var evt in events)
            if (evt.Venue == oldName)
                evt.Venue = newName;
    }

    internal static void RemoveOfVenue(this HashSet<Event> allEvents, string oldName)
        => allEvents.RemoveWhere(e => e.Venue == oldName);
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
}
