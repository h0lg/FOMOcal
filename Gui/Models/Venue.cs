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
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(ProgramUrl);

    public class EventScrapeJob
    {
        public required string Selector { get; set; }
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
    }
}
