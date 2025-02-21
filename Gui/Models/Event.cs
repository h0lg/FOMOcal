﻿namespace FomoCal;

public class Event
{
    public required string Name { get; set; }
    public string? SubTitle { get; internal set; }
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

    public string? PresalePrice { get; set; }
    public string? DoorsPrice { get; set; }
    public string? TicketUrl { get; set; }

    public override bool Equals(object? obj) => obj is Event other && Equals(other);
    public bool Equals(Event? other) => other is not null && GetHashCode() == other.GetHashCode();
    public override int GetHashCode() => HashCode.Combine(Venue, Url ?? Name, Date.Date);
}
