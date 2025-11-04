using System.Globalization;

namespace FomoCal;

public class DateScrapeJob : ScrapeJob
{
    private string culture = "en";
    private CultureInfo? cultureInfo;

    public string Format { get; set; } = "ddd dd MMM yyyy";

    public string Culture
    {
        get { return culture; }
        set
        {
            culture = value;
            cultureInfo = null;
        }
    }

    private CultureInfo CultureInfo => cultureInfo ??= new(Culture);

    public DateTime? GetDate(IDomElement element, List<Exception>? errors = null)
    {
        var rawValue = base.GetValue(element, errors);
        if (rawValue.IsNullOrWhiteSpace()) return null;
        const DateTimeStyles style = DateTimeStyles.None;
        string[] formats = Format.Split("||", StringSplitOptions.RemoveEmptyEntries);

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(rawValue, format, CultureInfo, style, out DateTime parsedDate)) return parsedDate;
        }

        foreach (var format in formats)
        {
            if (!format.Contains('y') // retry parsing date as next year's for format without year info
               && DateTime.TryParseExact(rawValue + " " + (DateTime.Today.Year + 1), format + " yyyy",
                CultureInfo, style, out DateTime nextYearsDate)) return nextYearsDate;
        }

        return AddOrThrow<DateTime?>(errors, new Error($"Failed to parse date '{rawValue}' using format/s '{Format}' in culture '{Culture}'."));
    }

    public override string? GetValue(IDomElement element, List<Exception>? errors = null) => GetDate(element, errors)?.ToString("D");
    public override bool Equals(object? obj) => obj is DateScrapeJob other && Equals(other);
    public bool Equals(DateScrapeJob? other) => base.Equals(other) && Format == other!.Format && Culture == other.Culture;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Format, Culture);
}
