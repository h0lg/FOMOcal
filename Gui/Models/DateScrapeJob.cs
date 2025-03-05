using System.Globalization;
using PuppeteerSharp;

namespace FomoCal;

public class DateScrapeJob : ScrapeJob
{
    public string Format { get; set; } = "dd MMM yyyy";
    public string Culture { get; set; } = "en";

    public async Task<DateTime?> GetDateAsync(IElementHandle element)
    {
        var rawValue = await base.GetValueAsync(element);
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

    public override async Task<string?> GetValueAsync(IElementHandle element) => (await GetDateAsync(element))?.ToString("D");
    public override bool Equals(object? obj) => obj is DateScrapeJob other && Equals(other);
    public bool Equals(DateScrapeJob? other) => base.Equals(other) && Format == other!.Format && Culture == other.Culture;
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Format, Culture);
}
