using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FomoCal.Gui.Resources;
using static FomoCal.Venue;

namespace FomoCal;

public class Venue
{
    public required string Name { get; set; }
    public string? Location { get; set; }
    public required string ProgramUrl { get; set; }
    public required EventScrapeJob Event { get; set; }
    public DateTime? LastRefreshed { get; set; }
    public bool SaveScrapeLogs { get; set; }

    /// <summary>Encoding overrides to use - for when <see cref="ProgramUrl"/>
    /// returns text in a different encoding than it claims.
    /// Access via <see cref="TryGetDirectHtmlEncoding(out string?)"/>
    /// or <see cref="TryGetAutomationHtmlEncoding(out string?)"/></summary>
    public string? Encoding { get; set; }

    /// <summary>Determines whether to use an <see cref="Encoding"/> override
    /// for HTML loaded directly from the serverand returns <paramref name="encoding"/> if so.</summary>
    internal bool TryGetDirectHtmlEncoding([MaybeNullWhen(false)] out string encoding)
        => TryGetEncodingAtIndex(0, out encoding);

    /// <summary>Determines whether to use an <see cref="Encoding"/> override for HTML passed across the JS bridge
    /// by an <see cref="Gui.ViewModels.AutomatedEventPageView"/> and returns <paramref name="encoding"/> if so.</summary>
    internal bool TryGetAutomationHtmlEncoding([MaybeNullWhen(false)] out string encoding)
        => TryGetEncodingAtIndex(1, out encoding);

    private bool TryGetEncodingAtIndex(int index, [MaybeNullWhen(false)] out string encoding)
    {
        if (Encoding.IsNullOrWhiteSpace())
        {
            encoding = null;
            return false;
        }

        var encondings = Encoding!.Split('|');
        encoding = encondings.Length == 1 ? Encoding : encondings[index];
        return true;
    }

    public override bool Equals(object? obj) => obj is Venue other && Equals(other);
    public bool Equals(Venue? other) => other is not null && GetHashCode() == other.GetHashCode();
    public override int GetHashCode() => HashCode.Combine(Name, ProgramUrl);

    internal string Serialize() => JsonSerializer.Serialize(this);
    internal Venue DeepCopy() => JsonSerializer.Deserialize<Venue>(Serialize())!;

    public class EventScrapeJob
    {
        public required string Selector { get; set; }
        public bool LazyLoaded { get; set; }

        // migrates the old WaitForJsRendering to the new LazyLoaded
        public bool WaitForJsRendering { set => LazyLoaded = value; }

        public string? Filter { get; set; }
        public PagingStrategy PagingStrategy { get; set; }
        public string? NextPageSelector { get; set; }
        public string? Comment { get; set; }
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

        internal bool LoadsMoreOnScrollDown() => PagingStrategy == PagingStrategy.ScrollDownToLoadMore;
        private bool WaitsForEvents() => LazyLoaded || LoadsMoreOnScrollDown();
        internal bool RequiresAutomation() => WaitsForEvents() || PagingStrategy.ClicksElementToLoad();

        internal bool LoadsMoreOrDifferentOnNextPage()
            => NextPageSelector.IsSignificant() && PagingStrategy.RequiresNextPageSelector();

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

    public enum PagingStrategy
    {
        [Description("all on the first page")]
        AllOnOne = 0,

        [Description("more by clicking")]
        ClickElementToLoadMore = 1,

        [Description("different by navigating link")]
        NavigateLinkToLoadDifferent = 2,

        [Description("more by scrolling down")]
        ScrollDownToLoadMore = 3,

        [Description("different by clicking")]
        ClickElementToLoadDifferent = 4
    }
}

internal static class VenueExtensions
{
    internal static T Migrate<T>(this T venues) where T : IEnumerable<Venue>
    {
        foreach (var venue in venues)
        {
            ScrapeJob[] scrapeJobs = [
                .. typeof(EventScrapeJob).GetProperties()
                    .Where(p => p.PropertyType.IsAssignableTo(typeof(ScrapeJob)))
                    .Select(p => p.GetValue(venue.Event))
                    .WithValue().Cast<ScrapeJob>()];

            foreach (var job in scrapeJobs)
                job.Replace = StringExtensions.MigrateInlinedReplacements(job.Replace);
        }

        return venues;
    }

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
            import.SaveScrapeLogs = false;
            existing.Add(import);
        }
    }

    internal static bool RequiresNextPageSelector(this PagingStrategy strategy)
        => strategy == PagingStrategy.NavigateLinkToLoadDifferent || strategy.ClicksElementToLoad();

    internal static bool ClicksElementToLoad(this PagingStrategy strategy)
        => strategy == PagingStrategy.ClickElementToLoadMore
        || strategy == PagingStrategy.ClickElementToLoadDifferent;

    internal static bool LoadsDifferentEvents(this PagingStrategy strategy)
        => strategy == PagingStrategy.NavigateLinkToLoadDifferent
        || strategy == PagingStrategy.ClickElementToLoadDifferent;

    internal static string? GetHelp(this PagingStrategy strategy)
        => HelpTexts.ResourceManager.GetString(nameof(PagingStrategy) + strategy.ToString());
}
