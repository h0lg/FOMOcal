namespace FomoCal.Gui.ViewModels;

partial class EventList
{
    public class EventView : IHaveAnEvent
    {
        public Event Model { get; }
        public bool IsPast { get; }

        #region read-only proxies
        public string Name => Model.Name;
        public string? SubTitle => Model.SubTitle;
        public string? Genres => Model.Genres;
        public string? Description => Model.Description;
        public string? Url => Model.Url;
        public string? ImageUrl => Model.ImageUrl;

        /// <inheritdoc cref="Event.Date" />
        public DateTime Date => Model.Date;

        public string? DoorsTime => Model.DoorsTime;
        public string? StartTime => Model.StartTime;

        public string Venue => Model.Venue;
        public string? Stage => Model.Stage;

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

        public override bool Equals(object? obj) => obj is EventView other && Equals(other);
        public bool Equals(EventView? other) => other is not null && GetHashCode() == other.GetHashCode();
        public override int GetHashCode() => Model.GetHashCode();
        public override string ToString() => Model.ToString(); // for easier debugging
    }
}
