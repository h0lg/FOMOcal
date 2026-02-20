using System.Globalization;
using System.Reflection;

namespace FomoCal;

public class DateScrapeJob : ScrapeJob
{
    public static readonly PropertyInfo[] Properties = [.. typeof(DateScrapeJob).GetProperties()];
    private static readonly PropertyInfo[] stringProperties = [.. Properties.Where(p => p.PropertyType == typeof(string))];
    public static readonly string[] PropertyNames = [.. Properties.Select(p => p.Name)];
    public static readonly string[] StringPropertyNames = [.. stringProperties.Select(p => p.Name)];

    private string culture = string.Empty;
    private CultureInfo? cultureInfo;
    private string[]? formats, formatsWithWeekDayButNoYear; // caches

    /// <summary>Init via setter e.g. for testing, update using <see cref="UpdateFormat(string)"/> to clear caches.</summary>
    public string? Format { get; set; }

    private string[]? Formats => formats ??= Format?.Split("||", StringSplitOptions.RemoveEmptyEntries);

    private string[]? FormatsWithWeekDayButNoYear => formatsWithWeekDayButNoYear
        ??= Formats?.Where(f => f.Contains("ddd") && !f.Contains('y')).ToArray();

    public string Culture
    {
        get { return culture; }
        set
        {
            culture = value;
            cultureInfo = null;
        }
    }

    private CultureInfo CultureInfo => Culture.IsSignificant() ? cultureInfo ??= new(Culture) : CultureInfo.InvariantCulture;

    public void UpdateFormat(string? value)
    {
        Format = value;
        formatsWithWeekDayButNoYear = formats = null; // clear caches
    }

    public DateTime? GetDate(IDomElement element, List<Exception>? errors = null)
    {
        var rawValue = base.GetValue(element, errors);
        if (string.IsNullOrWhiteSpace(rawValue)) return null;

        var formats = Formats;

        if (formats == null)
        {
            if (DateTime.TryParse(rawValue, CultureInfo, out var result))
                return result;
        }
        else
        {
            // try regular parsing
            var parsed = TryParseWithFormats(rawValue, formats);
            if (parsed.HasValue) return parsed;

            /* retry parsing for formats with week day but no year
             * for the next two years to avoid errors due to week day mismatches */
            if (FormatsWithWeekDayButNoYear!.Length > 0)
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

    protected override PropertyInfo[] StringProperties => stringProperties;
    public override string? GetValue(IDomElement element, List<Exception>? errors = null) => GetDate(element, errors)?.ToString("D");
    public override bool Equals(object? obj) => obj is DateScrapeJob other && Equals(other);
    public bool Equals(DateScrapeJob? other) => base.Equals(other) && Format == other!.Format && Culture == other.Culture;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Format, Culture);
}
