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
        CultureInfo culture = new(Culture);
        const DateTimeStyles style = DateTimeStyles.None;

        if (DateTime.TryParseExact(rawValue, Format, culture, style, out DateTime parsedDate)) return parsedDate;

        if (!Format.Contains('y') // retry parsing date as next year's for format without year info
            && DateTime.TryParseExact(rawValue + " " + (DateTime.Today.Year + 1), Format + " yyyy",
                culture, style, out DateTime nextYearsDate)) return nextYearsDate;

        throw new Error($"Failed to parse date '{rawValue}' using format '{Format}' in culture '{Culture}'.");
    }

    public override string? GetValue(AngleSharp.Dom.IElement element) => GetDate(element)?.ToString("D");
    public override bool Equals(object? obj) => obj is DateScrapeJob other && Equals(other);
    public bool Equals(DateScrapeJob? other) => base.Equals(other) && Format == other!.Format && Culture == other.Culture;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Format, Culture);
}
