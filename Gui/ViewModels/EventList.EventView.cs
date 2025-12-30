using CommunityToolkit.Mvvm.ComponentModel;

namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    public partial class EventView : ObservableObject, IHaveAnEvent
    {
        private IReadOnlyList<TextChunk>? name, subTitle, genres, description, venue, stage;
        private readonly PropertyChangeBatcher batcher; // used to batch PropertyChange notifications to reduce layout passes

        public Event Model { get; }
        public bool IsPast { get; }

        // searched and highlit text properties
        public IReadOnlyList<TextChunk>? Name { get => name; }
        public IReadOnlyList<TextChunk>? SubTitle { get => subTitle; }
        public IReadOnlyList<TextChunk>? Genres { get => genres; }
        public IReadOnlyList<TextChunk>? Description { get => description; }
        public IReadOnlyList<TextChunk>? Venue { get => venue; }
        public IReadOnlyList<TextChunk>? Stage { get => stage; }

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
            batcher = new PropertyChangeBatcher(name => OnPropertyChanged(name));
        }

        /// <summary>A custom setter for the searched and highlit text properties
        /// that minimizes PropertyChanged notifications by carefully comparing
        /// the set <paramref name="field"/> to the new <paramref name="value"/>
        /// and uses the <see cref="batcher"/> to dispatch them for the <paramref name="propertyName"/>.</summary>
        /// <returns>Whether the <paramref name="field"/> was updated.</returns>
        private bool Set<T>(ref IReadOnlyList<T>? field, List<T>? value, string propertyName)
        {
            // both null, no change
            if (field is null && value is null) return false;

            // both not null, check value equality
            if (field is not null && value is not null)
            {
                // both empty, no change
                if (field.Count == 0 && value.Count == 0) return false;

                // count and value equality means no change
                if (field.Count == value.Count && field.SequenceEqual(value)) return false;
            }

            // one null or not equal, changed
            field = value;
            batcher.Notify(propertyName);
            return true;
        }

        /// <summary>Chunks the searched text properties by the <paramref name="terms"/>
        /// to highlight the latter in the former.</summary>
        internal void SetSearchTerms(string[] terms)
        {
            using (batcher.Defer()) // batch property change notifications into one cycle to avoid intermediate layout passes
            {
                Set(ref name, Model.Name.ChunkBy(terms), nameof(Name));
                Set(ref subTitle, Model.SubTitle.ChunkBy(terms), nameof(SubTitle));

                /* prepend icons to bound chunks because a format string on the binding
                 * doesn't work when binding to Label.FormattedTextProperty */
                Set(ref genres, Model.Genres.ChunkBy(terms).PrependWith("🎶 "), nameof(Genres));
                Set(ref description, [.. Model.Description.ChunkByLinksAnd(terms)], nameof(Description));
                Set(ref venue, Model.Venue.ChunkBy(terms).PrependWith("🏟 "), nameof(Venue));
                Set(ref stage, Model.Stage.ChunkBy(terms).PrependWith("🏛 "), nameof(Stage));
            }
        }

        public override bool Equals(object? obj) => obj is EventView other && Equals(other);
        public bool Equals(EventView? other) => other is not null && GetHashCode() == other.GetHashCode();
        public override int GetHashCode() => Model.GetHashCode();
        public override string ToString() => Model.ToString(); // for easier debugging
    }
}
