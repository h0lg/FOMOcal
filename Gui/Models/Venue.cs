using System.Collections.ObjectModel;
using System.Text.Json;

namespace FomoCal;

public class Venue
{
    public required string Name { get; set; }
    public string? Location { get; set; }
    public required string ProgramUrl { get; set; }
    public required EventScrapeJob Event { get; set; }
    public DateTime? LastRefreshed { get; set; }

    public override bool Equals(object? obj) => obj is Venue other && Equals(other);
    public bool Equals(Venue? other) => other is not null && GetHashCode() == other.GetHashCode();
    public override int GetHashCode() => HashCode.Combine(Name, ProgramUrl);

    internal Venue DeepCopy() => JsonSerializer.Deserialize<Venue>(JsonSerializer.Serialize(this))!;

    public class EventScrapeJob
    {
        public required string Selector { get; set; }
        public bool ScrollDownToLoadMore { get; set; }
        public bool WaitForJsRendering { get; set; }
        public required ScrapeJob Name { get; set; }
        public required DateScrapeJob Date { get; set; }

        public ScrapeJob? SubTitle { get; set; }
        public ScrapeJob? Description { get; set; }
        public ScrapeJob? Genres { get; set; }
        public ScrapeJob? Stage { get; set; }

        public ScrapeJob? DoorsTime { get; set; }
        public ScrapeJob? StartTime { get; set; }

        public ScrapeJob? PresalePrice { get; set; }
        public ScrapeJob? DoorsPrice { get; set; }

        public ScrapeJob? Url { get; set; }
        public ScrapeJob? ImageUrl { get; set; }
        public ScrapeJob? TicketUrl { get; set; }

        internal bool WaitsForEvents() => WaitForJsRendering || ScrollDownToLoadMore;

        public override bool Equals(object? obj) => obj is EventScrapeJob other && Equals(other);

        public bool Equals(EventScrapeJob? other)
        {
            if (other is null) return false;

            return Selector == other.Selector
                && Name.Equals(other.Name)
                && Date.Equals(other.Date)
                && Equals(SubTitle, other.SubTitle)
                && Equals(Description, other.Description)
                && Equals(Genres, other.Genres)
                && Equals(Stage, other.Stage)
                && Equals(DoorsTime, other.DoorsTime)
                && Equals(StartTime, other.StartTime)
                && Equals(PresalePrice, other.PresalePrice)
                && Equals(DoorsPrice, other.DoorsPrice)
                && Equals(Url, other.Url)
                && Equals(ImageUrl, other.ImageUrl)
                && Equals(TicketUrl, other.TicketUrl);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Selector);
            hash.Add(Name);
            hash.Add(Date);
            hash.Add(SubTitle);
            hash.Add(Description);
            hash.Add(Genres);
            hash.Add(Stage);
            hash.Add(DoorsTime);
            hash.Add(StartTime);
            hash.Add(PresalePrice);
            hash.Add(DoorsPrice);
            hash.Add(Url);
            hash.Add(ImageUrl);
            hash.Add(TicketUrl);
            return hash.ToHashCode();
        }
    }
}

internal static class VenueExtensions
{
    internal static void Import(this Collection<Venue> existing, HashSet<Venue> imported)
    {
        foreach (var import in imported)
        {
            var local = existing.SingleOrDefault(v => v.ProgramUrl == import.ProgramUrl);

            if (local != null)
            {
                if (import.Event.Equals(local.Event)) continue;
                import.Name += $" (imported {DateTime.Now:g})";
            }

            import.LastRefreshed = null;
            existing.Add(import);
        }
    }
}
