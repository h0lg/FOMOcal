using System.Globalization;

namespace FomoCal;

public class DateScrapeJob : ScrapeJob
{
    private string culture = "en";
    private CultureInfo? cultureInfo;
    private string[]? formats, formatsWithWeekDayButNoYear; // caches

    /// <summary>Init via setter e.g. for testing, update using <see cref="UpdateFormat(string)"/> to clear caches.</summary>
    public string Format { get; set; } = "ddd dd MMM yyyy";

    private string[] Formats => formats ??= Format.Split("||", StringSplitOptions.RemoveEmptyEntries);

    private string[] FormatsWithWeekDayButNoYear => formatsWithWeekDayButNoYear
        ??= [.. Formats.Where(f => f.Contains("ddd") && !f.Contains('y'))];

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

    internal void UpdateFormat(string value)
    {
        Format = value;
        formatsWithWeekDayButNoYear = formats = null; // clear caches
    }

    public DateTime? GetDate(IDomElement element, List<Exception>? errors = null)
    {
        var rawValue = base.GetValue(element, errors);
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        // try regular parsing
        var parsed = TryParseWithFormats(rawValue, Formats);
        if (parsed.HasValue) return parsed;

        /* retry parsing for formats with week day but no year
         * for the next two years to avoid errors due to week day mismatches */
        if (FormatsWithWeekDayButNoYear.Length > 0)
        {
            var currentYear = DateTime.Today.Year;

            for (int offset = 1; offset <= 2; offset++)
            {
                parsed = TryParseWithFormats(rawValue, FormatsWithWeekDayButNoYear,
                    valueTransform: v => $"{v} {currentYear + offset}",
                    formatTransform: f => $"{f} yyyy");

                if (parsed.HasValue) return parsed;
            }
        }

        return AddOrThrow<DateTime?>(errors, new Error($"Failed to parse date '{rawValue}' using format/s '{Format}' in culture '{Culture}'."));
    }

    private DateTime? TryParseWithFormats(string raw, IEnumerable<string> formats,
        Func<string, string>? valueTransform = null, Func<string, string>? formatTransform = null)
    {
        var value = valueTransform?.Invoke(raw) ?? raw;

        foreach (var format in formats)
        {
            var usedFormat = formatTransform?.Invoke(format) ?? format;

            if (DateTime.TryParseExact(value, usedFormat, CultureInfo, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return null;
    }

    public override string? GetValue(IDomElement element, List<Exception>? errors = null) => GetDate(element, errors)?.ToString("D");
    public override bool Equals(object? obj) => obj is DateScrapeJob other && Equals(other);
    public bool Equals(DateScrapeJob? other) => base.Equals(other) && Format == other!.Format && Culture == other.Culture;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Format, Culture);
}
