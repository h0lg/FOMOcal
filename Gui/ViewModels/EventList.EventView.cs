namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    public class EventView : IHaveAnEvent
    {
        public Event Model { get; }
        public bool IsPast { get; }

        // searched and highlit text properties
        public IReadOnlyList<TextChunk>? Name { get; private set; }
        public IReadOnlyList<TextChunk>? SubTitle { get; private set; }
        public IReadOnlyList<TextChunk>? Genres { get; private set; }
        public IReadOnlyList<TextChunk>? Description { get; private set; }
        public IReadOnlyList<TextChunk>? Venue { get; private set; }
        public IReadOnlyList<TextChunk>? Stage { get; private set; }

        #region read-only proxies
        public string? Url => Model.Url;
        public string? ImageUrl => Model.ImageUrl;

        /// <inheritdoc cref="Event.Date" />
        public DateTime Date => Model.Date;

        public string? DoorsTime => Model.DoorsTime;
        public string? StartTime => Model.StartTime;

        /// <inheritdoc cref="Event.Scraped" />
        public DateTime Scraped => Model.Scraped;

        Event IHaveAnEvent.Event => Model;
        public string? ScrapedFrom => Model.ScrapedFrom;

        public string? PresalePrice => Model.PresalePrice;
        public string? DoorsPrice => Model.DoorsPrice;
        public string? TicketUrl => Model.TicketUrl;
        #endregion

        public EventView(Event e)
        {
            Model = e;
            IsPast = Date < DateTime.Today;
        }

        /// <summary>Chunks the searched text properties by the <paramref name="terms"/>
        /// to highlight the latter in the former.</summary>
        internal void SetSearchTerms(string[] terms)
        {
            Name = Model.Name.ChunkBy(terms);
            SubTitle = Model.SubTitle.ChunkBy(terms);

            /* prepend icons to bound chunks because a format string on the binding
             * doesn't work when binding to Label.FormattedTextProperty */
            Genres = Model.Genres.ChunkBy(terms).PrependWith("🎶 ");
            Description = [.. Model.Description.ChunkByLinksAnd(terms)];
            Venue = Model.Venue.ChunkBy(terms).PrependWith("🏟 ");
            Stage = Model.Stage.ChunkBy(terms).PrependWith("🏛 ");
        }

        public override bool Equals(object? obj) => obj is EventView other && Equals(other);
        public bool Equals(EventView? other) => other is not null && GetHashCode() == other.GetHashCode();
        public override int GetHashCode() => Model.GetHashCode();
        public override string ToString() => Model.ToString(); // for easier debugging
    }
}
