using System.Globalization;

namespace FomoCal;

public class DateScrapeJob : ScrapeJob
{
    public string Format { get; set; } = "dd MMM yyyy";
    public string Culture { get; set; } = "en";

    public DateTime? GetDate(AngleSharp.Dom.IElement element)
    {
        var rawValue = base.GetValue(element);
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        try
        {
            return DateTime.ParseExact(rawValue, Format, new CultureInfo(Culture));
        }
        catch (Exception ex)
        {
            throw new Error($"Failed to parse date '{rawValue}' using format '{Format}' in culture '{Culture}'.", ex);
        }
    }

    public override string? GetValue(AngleSharp.Dom.IElement element) => GetDate(element)?.ToString("D");
    public override bool Equals(object? obj) => obj is DateScrapeJob other && Equals(other);
    public bool Equals(DateScrapeJob? other) => base.Equals(other) && Format == other!.Format && Culture == other.Culture;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Format, Culture);
}
